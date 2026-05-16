# 17 - Effect 系统设计

> 2026-05-12 讨论 + 实施。已完成 4 阶段提交。

## 实施状态

**已实施** (4 commits on `refactor/combat-round-enemy-ai`):
- `d7641ea` — Function SO 替代枚举 + 类型重命名 + 字段精简 (22→12)
- `89b76bf` — SkillExecutor → AbilitySystemComponent 重命名
- `d4be821` — Tag 驱动冷却 + AttributeSet 触发器 + 被动 Ability 系统
- `66c975f` — 删除旧资产，Editor 脚本更新

**已知问题**:
- `Assets/Data/EffectFunctions/` 资产需通过 Unity Editor 菜单 `MiniChess/Create Basic Attack Assets` 创建
- ASC 组件 GUID 变更，场景中需重新挂载 `AbilitySystemComponent`
- `GameplayTagSet.Add/Remove` 改为返回 bool（影响调用方）
- `AdvanceCooldownsFunction.Apply` 现为空操作（冷却由 ActiveSkillEffect 生命周期管理）

## 2026-05-16 设计修订摘要

本轮不推翻 Function SO 方案，只补齐 GAS-like 边界：

1. 恢复轻量 `SkillDefinition` / `SkillMetadata`，用于 UI、AI、预览和配置校验读取技能身份、目标类型、范围、AI tags、表现引用和 Ability 引用。
2. `SkillAbility` 只负责流程编排，必须保持无运行时状态。
3. ASC 持有运行时 `AbilitySpec`，可用技能配置入口为 `SkillDefinition[]`，不直接持有裸 Ability SO 列表。
4. `SkillEffect` 增加 `ESkillEffectTarget` 目标映射，Cost/Cooldown 默认检查 Caster，伤害/治疗默认检查 Target。
5. Persistent Effect 拆成 OnApply / OnTick / OnRemove 生命周期，不能复用同一个 Apply 同时表达添加、tick 和移除。
6. `ActiveSkillEffect` 实例作为 GameplayTag source handle，禁止使用 `"Effect.{effect.name}"` 字符串作为持续 Tag 来源。
7. 冷却仍是普通 Persistent Effect，但使用 `TagOnlyEffectFunction` 或等价空行为 Function，不能绕过 `SkillEffect.Compute()`。

## 历史问题：枚举 Dispatcher

```
EffectDefinition (sealed SO)
  └── m_function: EEffectFunction 枚举 (16 个值)
        → EffectFunctionDispatcher switch 路由到静态函数类
```

**已解决的问题：**
- 新增行为 = 改枚举 + 改 Dispatcher switch → 违反开闭原则
- Dispatcher 维护 switch 和 guard 两套重复列表
- 所有 Function 的参数平铺在 EffectDefinition 上（amount, attributeTag, cooldownSkillId, destroyDelaySeconds...），配任意一种 Function 都会看到其他 Function 的无关字段

---

## 决议 1：Function SO 替代枚举

### 方案选择

曾考虑 Tag + `[RuntimeInitializeOnLoadMethod]` 自动注册方案（见下文废弃方案），**最终选择多态 Function SO**。

### 最终方案：多态 Function SO

```
EffectFunctionBase (abstract SO)
  ├── abstract Compute(ctx, effect) → EffectResult
  ├── abstract Apply(ctx, effect, result) → void
  └── 各自携带专属参数

EffectDefinition
  └── m_function: EffectFunctionBase  ← 挂一个 SO 引用
```

每个函数实现 = 一个 SO 子类 + 一个 `.asset` 文件：

```
Assets/Data/EffectFunctions/
  spend_ap.asset               → SpendAPFunction (无参数，或可选 amount 兜底)
  modify_attribute.asset       → ModifyAttributeFunction (magnitude, attributeTag)
  set_cooldown.asset           → SetCooldownFunction (skillId, rounds)
  restore_attribute.asset      → RestoreAttributeFunction (mode, attributeTag)
  destroy_gameobject.asset     → DestroyGameObjectFunction (delaySeconds)
  reset_movement.asset         → ResetMovementFunction (无参数)
  advance_cooldowns.asset      → AdvanceCooldownsFunction (无参数)
  ...
```

EffectDefinition 执行时直接 `m_function.Compute(ctx, this)` / `m_function.Apply(ctx, this, result)`——**没有枚举，没有 switch，没有 Dispatcher**。

### 为什么不用 Tag + 自动注册

Tag + `[RuntimeInitializeOnLoadMethod]` 方案（见下方废弃设计）虽然解决了枚举扩展问题，但**无法解决参数平铺问题**——静态类不带参数，EffectDefinition 上仍然要保留 amount / attributeTag / restoreMode / cooldownSkillId / destroyDelaySeconds 等所有字段，配置一个 Function 时仍会看到无关参数。

多态 Function SO 方案一举解决两个问题：
1. 新增 Function = 新建 SO 子类 + 资产，不改已有代码
2. 参数跟着 Function 走——`SetCooldownFunction` 身上只有 `cooldownSkillId`/`cooldownRounds`，不会有 `destroyDelaySeconds`

### 删除项

- `EEffectFunction.cs` — 枚举
- `EffectFunctionDispatcher.cs` — switch 路由

---

## 决议 2：数值计算归属 Function

- `m_amount`、`m_restoreMode`、`m_attributeTag` 等数值相关参数从 EffectDefinition 移除，归属到各自 Function SO 上
- 接受"公式在代码里，不可通过资产自定义公式"——数值计算能力由 Function 类型提供，最大程度兼容各种复杂公式
- 不引入 UE GAS 的 Magnitude 计算策略（ScalableFloat/AttributeBased/SetByCaller 等），当前规模不需要，后期有需求再评估

## 决议 3：冷却 = 普通持久 Effect

- 冷却不需要 `SetCooldownFunction` 专属逻辑
- 冷却 = Persistent Effect + `TagOnlyEffectFunction` + GrantedTags (Cooldown.xxx) + DurationRounds
- SkillAbility 上的 `m_cooldowns[]` 槽位保留（概念特殊且通用，不适合混入 Effects[]）
- Cooldown 槽位执行前自动检查 GrantedTags 是否已存在 → 已存在则阻止释放；检查仍要经过统一 `SkillEffect.Compute()`，不得作为绕过 Effect 目标映射和 Tag 条件的特殊旁路
- **不自动绑定** grantedTag == blockedTag，保持通用，信任配置

### 删除项

- `EEffectFunction.SetCooldown`
- `SetCooldownFunction` 类
- EffectDefinition 上的 `m_cooldownSkillId`、`m_cooldownRounds`

### 冷却配置示例

```
power_strike_cooldown:
  Function: TagOnlyEffectFunction
  Duration: Persistent
  DurationRounds: 2
  TargetMapping: Caster
  GrantedTags:
    - Cooldown.PowerStrike
```

## 决议 4：死亡流程 = 被动 Ability + Tag 触发

### 入口收敛

两种死亡入口汇合到一个 Tag：

```
路径 1 (HP 归零):
  AttributeSet.Modify(HP, delta) → 值 ≤ 0
    → AttributeSet 触发规则: 加 Status.Dead Tag

路径 2 (即死技能):
  技能 Effect → GrantedTags: [Status.Dead]
```

### AttributeSet 阈值触发器

参考 UE `PostGameplayEffectExecute`，AttributeSet 在 `Modify()` 后检查可配置的触发规则：

```csharp
[System.Serializable]
public struct AttributeTriggerRule
{
    public GameplayTag Attribute;       // Attribute.HP
    public ETriggerCondition Condition; // LessOrEqual
    public float Threshold;             // 0
    public GameplayTag[] AddTags;       // [Status.Dead]
}
```

AttributeSet 保持 MonoBehaviour（继承 UE 的 AttributeSet 作为数据+规则容器的思路），后期考虑数据化。

### 被动 Ability 触发器

- SkillExecutor 已重命名并扩展为 **AbilitySystemComponent (ASC)**
- ASC 管理被动 Ability 列表（带 `m_activationTrigger` Tag）
- ASC 监听 GameplayTagComponent 的 Tag 添加事件
- Tag 匹配时自动执行对应 Ability

```
GameplayTagComponent.AddTag(Status.Dead)
  → ASC.OnTagAdded(Status.Dead)
    → 找到 Death Ability (trigger: Status.Dead)
      → Execute: DeregisterFromCombat → DeathVisual → DestroyGameObject
```

### 删除项

- `EEffectFunction.DeregisterFromCombat`、`DeathVisual`、`DestroyGameObject` — 不再作为独立 Effect Function，而是死亡 Ability 内部的 Effect 链

---

## 参数整理：最终结论

EffectDefinition 保留/移除的字段总结：

| 字段 | 归属 | 状态 |
|------|------|------|
| m_function | → Function SO | ✅ |
| m_amount | → Function SO | ✅ |
| m_attributeTag | → Function SO | ✅ |
| m_restoreMode | → Function SO | ✅ |
| m_targetMapping | EffectDefinition (框架层) | ✅ |
| m_cooldownSkillId | ❌ 删除 | ✅ |
| m_cooldownRounds | ❌ 删除 | ✅ |
| m_destroyDelaySeconds | ❌ 删除 | ✅ |
| m_tickPhase | ❌ 删除 | ✅ |
| m_requiredCapability | ❌ 删除 | ✅ |
| m_stackRule | ❌ 删除 | ✅ |
| m_requiredTags / m_blockedTags | EffectDefinition (框架层) | ✅ |
| m_grantedTags / m_removeTags | EffectDefinition (框架层) | ✅ |
| m_duration / m_durationRounds | EffectDefinition (框架层) | ✅ |
| m_tickPerRound | EffectDefinition (框架层) | ✅ |
| m_grantedAbilities | EffectDefinition (框架层) | ✅ |
| m_statModifiers | EffectDefinition (框架层) | ✅ |
| m_tags | EffectDefinition (元数据) | ✅ |
| m_description | EffectDefinition (元数据) | ✅ |

### 精简后 EffectDefinition 保留字段

```
Parameters:
  (无 — 全部迁至 Function SO)

Target:
  m_targetMapping (Caster / Target / Source / Self / GroundPoint)

Duration:
  m_duration (Instant/Persistent)
  m_durationRounds
  m_tickPerRound

Tag Interactions:
  m_requiredTags[]
  m_blockedTags[]
  m_grantedTags[]
  m_removeTags[]

持久附加:
  m_grantedAbilities[]
  m_statModifiers[]

元数据:
  m_tags[]
  m_description

Function:
  m_function: EffectFunctionBase  ← SO 引用
```

从 **22 个字段 → 13 个字段**（其中 1 个是 Function SO 引用，1 个是目标映射）。

---

## 决议 5：Effect 目标映射

每个 `SkillEffect` 必须显式表达目标映射：

```csharp
public enum ESkillEffectTarget
{
    Caster,
    Target,
    Source,
    Self,
    GroundPoint
}
```

默认配置约定：

| 槽位 / 效果类型 | 默认目标 |
|---|---|
| Costs，如 `SpendAP` | `Caster` |
| Cooldowns | `Caster` |
| Damage / Heal | `Target` |
| Self Buff | `Caster` |
| 地面效果 | `GroundPoint` |

`RequiredTags` / `BlockedTags` 检查映射后的对象。这样 `SpendAPEffect.BlockedTags = [State.APBlocked]` 会检查施法者，而不是目标；`DamageEffect.BlockedTags = [State.Immune.Fire]` 才检查受击目标。

---

## 决议 6：Persistent Effect 生命周期

Persistent Effect 不再复用同一个 `Apply()` 承担添加、回合 tick 和移除三种语义。ASC 管理 `ActiveSkillEffect` 生命周期：

```csharp
public sealed class ActiveSkillEffect
{
    public SkillEffect Definition;
    public GameObject Source;
    public AbilitySystemComponent Owner;
    public int RemainingRounds;
    public int StackCount;
    public object TagSourceHandle; // usually this ActiveSkillEffect instance
}
```

生命周期：

| 阶段 | 职责 |
|---|---|
| `OnApply` | 注册 ActiveSkillEffect、添加 GrantedTags、授予 AbilitySpec、应用属性修正、执行进入效果 |
| `OnTick` | 执行周期效果，例如毒伤、持续治疗、冷却/状态持续回合递减 |
| `OnRemove` | 用 ActiveSkillEffect handle 移除 GrantedTags、撤销授予 AbilitySpec、按快照回滚属性修正、执行退出效果 |

Tag 来源必须使用 `ActiveSkillEffect` 实例或稳定 handle。禁止使用 `Effect.name`、`Effect.{effect.name}`、Skill id 字符串作为持续 Tag source；否则同一 Effect 多次叠加时，GameplayTagSet 会把多层状态视为同一个来源。

### StackRule 最小集

第一阶段只要求支持：

| 规则 | 行为 |
|---|---|
| `RefreshDuration` | 已存在同源/同定义状态时刷新持续时间 |
| `AddStack` | 新增一层，`StackCount++`，每层需要可独立回滚 |
| `IgnoreIfExists` | 已存在时不重复添加 |

属性修正必须记录应用快照，移除时按快照回滚，不能按当前 MaxValue 重新计算百分比修正。

---

## 决议 7：AbilitySpec 运行时实例

ASC 的可用技能从 `SkillDefinition[]` 构建为 `AbilitySpec[]`：

```csharp
public sealed class AbilitySpec
{
    public SkillDefinition Definition;
    public SkillAbility Ability;
    public GameObject Source;
    public object GrantSource;
    public int Level;
}
```

`SkillAbility` 是共享 SO，必须无状态；`AbilitySpec` 承载来源、等级、授予关系和未来的运行时扩展。状态授予技能、装备授予技能、临时禁用技能都通过 AbilitySpec 表达。旧裸 `SkillAbility[]` 可用技能列表不再保留兼容入口。

---

## 命名统一

| 原名 | 新名 | 说明 |
|------|------|------|
| `SkillExecutor` | `AbilitySystemComponent` | ASC，单位的技能系统核心 |
| `EffectDefinition` | `SkillEffect` | SkillEffect 资产 |
| `EffectContext` | `SkillEffectContext` | Effect 执行上下文 |
| `EffectResult` | `SkillEffectResult` | Effect 计算结果 |
| `ActiveEffect` | `ActiveSkillEffect` | 运行时持续效果实例 |
| `EEffectDuration` | `ESkillEffectDuration` | Instant / Persistent |
| `EffectFunctionBase` | `SkillEffectFunction` | Function SO 抽象基类 |
| `ERestoreMode` | → 内聚到 RestoreAttributeFunction | 不再独立暴露 |
| `EStatusTickPhase` | ❌ 随 m_tickPhase 删除 | — |
| `ETargetCapability` | ❌ 随 m_requiredCapability 删除 | — |
| `EEffectFunction` | ❌ 删除 | — |
| `EffectFunctionDispatcher` | ❌ 删除 | — |

---

## 附录：废弃的 Tag 自动注册方案

```
已废弃，保留供参考。

EffectFunctionRegistry (静态字典: Tag hash → (Compute, Apply) delegate)
  └── 每个静态函数类通过 [RuntimeInitializeOnLoadMethod] 自注册

EffectDefinition
  └── m_functionTag: GameplayTag  ← 替代枚举

废弃原因：只解决了枚举扩展问题，没有解决参数归属问题。
参数仍全部平铺在 EffectDefinition 上，Inspector 噪音依旧。
```

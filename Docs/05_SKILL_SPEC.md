# 05 - 技能规格 (Skill Spec)

> 技能是战斗中除"基础移动"之外的所有主动行为。每个技能自定义 AP 消耗、目标、范围、效果。

## 1. 技能字段

> **设计修订 (2026-05-16):** 技能系统恢复轻量 `SkillDefinition` / `SkillMetadata` 数据层。`SkillAbility` 不再单独承担"技能是什么"与"技能怎么执行"两种职责：前者由 `SkillDefinition` 暴露给 UI、AI、预览和配置校验读取，后者由 `SkillAbility` 负责流程编排。`SkillDefinition` 只保存静态元数据与 Ability 引用，不保存 AP、冷却或任何运行时状态。

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | string | 唯一 ID |
| `displayName` | string | 显示名 |
| `description` | string | 给策划/调试 UI 使用的简短说明 |
| `targetType` | enum | `Self` / `SingleEnemy` / `SingleAlly` / `GroundPoint` / `Area` |
| `range` | float | 施放距离（米） |
| `areaShape` | enum | `Single` / `Circle(r)` / `Cone(angle, r)` / `Line(width, length)` |
| `requiresLineOfSight` | bool | 是否需要视线检查（MVP 可先不实现，保留字段） |
| `endsTurnAfterCast` | bool | 使用后是否强制结束该单位回合；默认 false |
| `skillTags` | GameplayTag[] | 技能语义标签，例如 `Skill.Attack.Basic`、`Skill.Heal.Minor` |
| `aiTags` | GameplayTag[] | AI 决策标签，例如 `Skill.Damage` / `Skill.Heal` / `Skill.Buff` / `Skill.Protect` |
| `aiBaseWeight` | float | AI 默认评分权重；只影响敌方决策，不影响玩家使用 |
| `animation` | AnimationClipRef | 表现层 |
| `vfx` | GameObject | 表现层 |
| `ability` | SkillAbility | 执行流程资产。必须显式配置，不允许空 Ability 走隐藏默认流程 |

> **设计决议 (2026-05-12 / 2026-05-16 修订):** `SkillDefinition.apCost` 与 `SkillDefinition.cooldown` 不再作为设计字段保留。当前仍处设计阶段，资产依赖较少，直接删除旧字段，避免后续重构出现 `SkillDefinition` 字段与 Ability/Effect 槽位并存的双轨逻辑。AP 消耗通过 Ability 的 `Costs` 槽位配置 `SpendAPEffect`；冷却通过 Ability 的 `Cooldowns` 槽位配置 Persistent `SkillEffect` + `TagOnlyEffectFunction`，并使用 `Cooldown.{skillId}` Tag 表达。

### 1.0 架构边界

技能系统分为四层：

| 层 | 职责 | 运行时状态 |
|---|---|---|
| `SkillDefinition` / `SkillMetadata` | 技能静态身份、目标类型、范围、AI/UI 可读信息、Ability 引用 | 无 |
| `SkillAbility` | 执行流程脚本，编排 Cost、Cooldown、Effect、移动或特殊逻辑 | 无，必须视为共享 SO |
| `SkillEffect` | 效果数据、Function 引用、Tag 条件、目标映射、生命周期配置 | 无 |
| `AbilitySystemComponent` | 单位运行时技能实例、授予技能、冷却、持续效果、Tag 来源、状态栈 | 有 |

任何 UI、AI、技能预览、配置校验都应优先读取 `SkillDefinition` 和 `SkillPreviewResult`，不应通过执行 `SkillAbility` 才知道目标类型、射程或 AI 语义。

### 1.0.1 AbilitySpec

ASC 不直接持有裸 `SkillAbility[]` 可用技能列表。运行时可用技能由 Inspector 显式配置的 `SkillDefinition[]` 生成 `AbilitySpec`：

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

第一阶段 `Level` 可固定为 1，暂不做充能、技能等级成长或装备技能来源复杂规则。引入 `AbilitySpec` 的目的是保证所有运行时状态留在 ASC，而不是写入共享 ScriptableObject 资产。
`SkillExecutionContext` 携带 `AbilitySpec`，`SkillAbility` 仅通过 `Spec.Ability` 作为执行流程被调用。

### 1.1 SkillAbility

> **迁移决议 (2026-05-11):** SkillAbility 接口从 `GetApCost+CanApply+Apply+HandleInput` 精简为单一 `Execute(context)`。新增 Costs/Cooldowns/Effects/BlockedTags 四个槽位。HandleInput 职责移出到 UnitTurnHandler。详见 `OPEN_QUESTIONS.md` §Ability-Effect 设计决议。

职责划分：

- `AbilitySystemComponent`：通用入口，负责施法者存活、冷却、目标类型、目标能力、阵营、射程等通用校验，并持有运行时 `AbilitySpec` / `ActiveSkillEffect`。ASC 的可用技能配置入口是 `SkillDefinition[]`，不是裸 `SkillAbility[]`。
- `SkillAbility`：开放给用户编写的流程类。基类提供通用 Tag / Cost / Cooldown / Effect helper；具体 Ability 显式调用这些 helper，自行编排“检查 → Ability 流程 → 应用”的顺序。结构：
  ```
  ├── BlockedTags[]    ← 释放者命中任一 → 不能释放
  ├── Costs: Effect[]  ← Compute(算消耗)→Execute时Apply(扣)
  ├── Cooldowns: Effect[] ← Compute(在冷却?)→Execute时Apply(设冷却)
  └── Effects: Effect[]   ← 实际游戏效果
  ```
- `EffectDefinition`：纯数据配置。Ability 在合适时机直接调用 Effect 配置的静态 `EffectFunction` 执行 `Compute` / `Apply`。

所有可释放 `SkillDefinition` 必须显式配置 Ability。`basic_attack` 也应挂载类似 `SimpleTargetAbility` / `MeleeAttackAbility` 的显式 Ability，不允许 `Ability == null` 时由 ASC 走隐藏默认流程。

`SkillAbility` 必须按无状态共享资产设计。不得在 Ability SO 上记录当前施法者、当前目标、剩余冷却、连击次数、运行时缓存路径或本次释放结果。此类数据属于 `SkillExecutionContext`、`AbilitySpec` 或 ASC。

`Costs` / `Cooldowns` / `Effects` 是 `SkillAbility` 基类的标准槽位，但不是强制执行规则。具体 Ability 可以选择性使用它们：

- 普通攻击：使用 `Costs` + `Effects`。
- 主动移动：使用 `Costs`，不需要 `Effects`。
- 系统技能：可能只使用 `Effects`。
- 纯流程 Ability：可以不使用这些槽位；是否合理交给 Ability 作者/使用者判断。

当前阶段，`SkillAbility` 基类不自动执行 `Costs -> Cooldowns -> Effects` 模板，只提供显式 helper：

```csharp
protected bool CheckAbilityTags(SkillExecutionContext context);
protected EffectResult[] ComputeCosts(SkillExecutionContext context);
protected void ApplyCosts(SkillExecutionContext context, EffectResult[] results);
protected EffectResult[] ComputeCooldowns(SkillExecutionContext context);
protected void ApplyCooldowns(SkillExecutionContext context, EffectResult[] results);
protected EffectResult ComputeEffect(SkillExecutionContext context, EffectDefinition effect);
protected void ApplyEffect(SkillExecutionContext context, EffectDefinition effect, EffectResult result);
```

后续如果发现大多数 Ability 都共享同一个流程，再把通用顺序提取为自动流程或模板方法。

#### 1.1.1 Tag 条件分层

`SkillDefinition` 层 Tag 条件保留，用于技能级全局判断：不满足时，技能不释放，Cost 不支付，Cooldown 不设置，Effect 全部不执行。典型用途包括沉默不能施法、缴械不能释放武器技能、隐身技能要求施法者拥有 `State.Hidden`。

`Ability` 层 Tag 条件用于动作流程权限，例如 `GroundMoveAbility.BlockedTags = [State.Rooted]` 表示移动流程不能启动。

`CostEffect` 层 Tag 条件用于资源支付规则，例如 `SpendAPEffect.BlockedTags = [State.APBlocked]` 表示 AP 支付被阻断，`State.FreeMove` / `State.FreeCast` 可用于免除或修改支付。

`EffectDefinition` 层 Tag 条件用于单个效果是否生效，例如 `DamageEffect.BlockedTags = [State.Immune.Fire]` 只阻止火焰伤害，不一定阻止同一技能里的其他效果。

同一语义不应同时配置在 Ability 和 CostEffect 上。重复 Tag 会造成失败原因不稳定、预览与执行分叉、配置维护重复。配置校验规则：

- Ability 与 CostEffect 出现相同 Required/Blocked Tag：Warning，提示语义重复。
- 同一作用域内 RequiredTags 与 BlockedTags 出现相同 Tag：Error，视为配置冲突。
- 如果一个状态同时影响流程权限和资源支付，应拆成更明确的 Tag，例如 `State.Rooted`、`State.APBlocked`、`State.FreeMove`。

### 1.2 输入请求与技能激活

玩家输入不直接调用具体技能，也不直接构造移动路径。`InputController` 只负责把原始输入翻译成 `SkillInputRequest`：

- `SignalTag`：输入信号，例如 `Input.Pointer.Hover`、`Input.Pointer.PrimaryPressed`。
- `TargetTag`：命中目标语义，例如 `Input.Target.Ground`、`Input.Target.Unit.Player`、`Input.Target.Unit.Enemy`。
- 参数：命中对象、世界坐标等强类型数据。

`CombatRoundManager` 负责把输入请求分发给当前选中角色的 ASC。当前 MVP 规则是：选中角色后自动激活该角色自己的 `basic_move`；后续 UI 技能栏只需要改为激活其他 `SkillDefinition`。ASC 保存当前激活技能，并把输入请求写入 `SkillExecutionContext` 后调用该技能的 `SkillAbility.Execute(context)`。

因此，输入层只表达“发生了什么输入”，技能层决定“这个输入对当前激活技能意味着什么”。

每个可行动角色必须在自身的 ASC 上配置可用技能资产。没有配置技能的角色不能通过全局管理器获得默认行动；`CombatRoundManager` 只能报告缺失配置，不能自动补发技能。运行时生成的敌人必须由生成源（当前是 `EnemySpawner.m_defaultSkills`）把技能列表写入新生成对象的 ASC。
`EnemySpawner.m_defaultSkills` 类型为 `SkillDefinition[]`。

## 2. 效果 (Effect) 结构

> **迁移决议 (2026-05-12):** Effect 是统一的纯数据 ScriptableObject，不允许通过继承 `DamageEffectDefinition : EffectDefinition`、`HealEffectDefinition : EffectDefinition` 等用户子类扩展行为。Effect 资产只保存静态 `EffectFunction` 引用、参数、Tag、条件、目标映射等配置。一个 `EffectFunction` 同时提供 `Compute(ctx, effectData, parameters)→EffectResult` 与 `Apply(ctx, effectData, parameters, result)`；参数通过 Effect 数据传入。自定义计算/应用逻辑通过新增静态 `EffectFunction` 完成，而不是继承 Effect 类；当前不引入 `EffectRunner`、`EffectOperation`、`HandlerId` 这类额外分发层。

### 2.1 基本数据

```csharp
public sealed class EffectDefinition : ScriptableObject
{
    [SerializeField] private EffectFunctionRef m_function;
    [SerializeField] private EffectParameterSet m_parameters;
    [SerializeField] private ESkillEffectTarget m_targetMapping;
    [SerializeField] private EEffectDuration m_duration;
    [SerializeField] private GameplayTag[] m_tags;
    [SerializeField] private GameplayTag[] m_grantedTags;
    [SerializeField] private GameplayTag[] m_removeTags;
    [SerializeField] private GameplayTag[] m_requiredTags;
    [SerializeField] private GameplayTag[] m_blockedTags;
}
```

调用示意：

```csharp
public sealed class EffectDefinition : ScriptableObject
{
    public EffectResult Compute(EffectContext context)
    {
        return m_function.Compute(context, this, m_parameters);
    }

    public void Apply(EffectContext context, EffectResult computed)
    {
        m_function.Apply(context, this, m_parameters, computed);
    }
}
```

`EffectFunctionRef` 是配置层对预定义静态函数类的选择入口。Unity 不能直接序列化“静态函数指针”，实现时可以用轻量枚举、ScriptableObject 函数引用或编辑器下拉选择来承载。设计语义是：Effect 本身不写逻辑、不派生子类，只选择一个完整函数类并提供参数。

`EffectFunction` 示例：`SpendAP`、`TagOnly`、`ModifyAttribute`、`AddStatus`、`ForcedMove`、`PullTarget`、`RestoreAttribute`、`ResetMovement`、`TriggerStatusTick`、`DestroyGameObject`。

### 2.0.1 Effect 目标映射

每个 `SkillEffect` 必须明确声明它作用于谁：

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

默认约定：

| 槽位 / 效果类型 | 默认目标 |
|---|---|
| Costs，如 `SpendAP` | `Caster` |
| Cooldowns | `Caster` |
| Damage / Heal | `Target` |
| Self Buff | `Caster` |
| 地面效果 | `GroundPoint` |

`RequiredTags` / `BlockedTags` 检查目标映射后的对象，而不是固定检查 `context.Target`。例如 `SpendAPEffect.BlockedTags = [State.APBlocked]` 必须检查施法者；火焰伤害的 `BlockedTags = [State.Immune.Fire]` 才检查受击目标。

### 2.1.1 Compute 失败语义

- `Costs` / `Cooldowns` 的 `Compute` 失败会阻止整个技能执行，不 Apply 任何 Cost、Cooldown 或普通 Effect。
- 普通 `Effects` 的 `Compute` 失败不阻止技能执行，只影响该 Effect 自身是否能释放到目标上；其他 Effect 继续按 Ability 流程执行。
- `FailurePolicy` / `isRequired` 这类“普通 Effect 失败是否反过来阻塞技能”的细粒度策略暂不进入当前实现，列为后续扩展设计。

Cost 的 `Apply` 也必须返回成功/失败结果，不能静默忽略资源扣除失败。默认主动技能流程为：通用校验 → ComputeCosts → ComputeCooldowns → ApplyCosts → ApplyCooldowns → ApplyEffects。需要特殊时序的技能可以由专门 Ability 明确覆盖，但不能让默认单目标技能先造成效果再扣费。

### 2.2 Tag 互作用字段

每个 Effect 自身携带四个 Tag 字段，表达它对目标 Tag 的交互：

| 字段 | 作用 | 示例 |
|------|------|------|
| **Identity Tags** (`m_tags`) | Effect 的身份标记 | `Effect.Damage.Physical`，用于免疫判断、AI 决策、debug |
| **GrantTags** (`m_grantedTags`) | Apply 时给目标添加 Tag。Persistent Effect 过期时自动清理 | `GuardingShout` 添加 `State.Guarded` |
| **RemoveTags** | Apply 时从目标移除匹配的 Tag | 清除 `State.Burning` 或解除标记 |
| **RequiredTags** | Compute 时检查目标必须拥有的 Tag | `ExecuteSkill` 要求目标有 `State.Weakened` |
| **BlockedTags** | Compute 时检查目标不能有的 Tag | `FireballDamage` 被 `State.Immune.Fire` 阻断 |

### 2.3 瞬时 vs 持久

| 类型 | 枚举值 | 生命周期 | 示例 |
|------|--------|---------|------|
| **Instant** | `EEffectDuration.Instant` | 执行后立即完成，不追踪 | `DamageEffect`、`HealEffect` |
| **Persistent** | `EEffectDuration.Persistent` | 注册到目标 ASC，按 OnApply / OnTick / OnRemove 生命周期管理 | `BurningStatus`、`GuardingBuff` |

`IsPersistent` 判定：`m_durationRounds > 0` 或 `GrantedAbilities.Length > 0` 或 `GrantedTags.Length > 0`。

Persistent Effect 不应复用同一个 `Apply()` 同时表达添加、tick 和移除。运行时生命周期拆为：

| 阶段 | 职责 |
|---|---|
| `OnApply` | 注册 `ActiveSkillEffect`、添加 GrantedTags、授予 Ability、应用属性修正、执行一次性进入效果 |
| `OnTick` | 按回合或时间触发周期效果，例如中毒伤害、持续治疗 |
| `OnRemove` | 按 `ActiveSkillEffect` source handle 移除 GrantedTags、撤销授予 Ability、按快照回滚属性修正、执行退出效果 |

`ActiveSkillEffect` 实例必须作为 Tag source handle。不要使用 `"Effect.{effect.name}"` 这类字符串作为来源，否则同一个状态叠多层时会互相覆盖，移除一层可能错误清掉或保留 Tag。
普通技能效果与冷却效果都必须通过目标 ASC 的 Effect 生命周期入口应用；不得直接调用 `SkillEffect.Apply` 后跳过 `ActiveSkillEffect` 注册。

### 2.4 预期函数类型

- **Costs:** `SpendAP`（Compute 检查/计算消耗，Apply 扣 AP）
- **Cooldowns:** `TagOnly` + Persistent Effect（Compute 走统一 Effect 入口，Apply 注册冷却 Tag，不能绕过 `SkillEffect.Compute`）
- **属性修改:** `ModifyAttribute`、`RestoreAttribute`
- **Status:** `AddStatus`、`RemoveStatus`
- **强制位移:** `ForcedMove`、`PullTarget`、`TeleportTarget`
- **系统:** `ResetMovement`、`AdvanceCooldowns`、`TriggerStatusTick`、`DecrementStatusDuration`、`DeregisterFromCombat`、`DeathVisual`、`DestroyGameObject`

## 3. 状态 (Status) 结构

状态用于表达 buff / debuff，不直接等同于技能。

建议字段：

| 字段 | 说明 |
|---|---|
| `id` | 唯一 ID |
| `displayName` | 显示名 |
| `durationRounds` | 持续轮数 |
| `stackRule` | `RefreshDuration` / `StackValue` / `IgnoreIfExists` |
| `statModifiers` | 对 Attack / Defense / MoveSpeed / MaxAP 等的临时修正 |
| `tags` | `Buff` / `Debuff` / `Control` / `DamageOverTime` / `Protect` |

MVP 阶段优先支持数值型状态，例如攻击提高、防御提高、受到持续伤害。眩晕、魅惑、改先攻等强控制先不做，避免破坏回合系统。

### 3.1 Status 与 Effect 的关系

- `Effect` 是一次性执行的结果单元，例如造成伤害、恢复 HP、添加状态。
- `Status` 是挂在单位身上的持续性运行时状态，例如中毒、持续恢复、防御提高。
- 技能本身不直接实现“每回合毒伤”或“每回合恢复”；技能通过 `AddStatusEffect` 添加一个 Status。
- Status 可以在特定时机触发自己的 Effect，例如：
  - 回合开始：触发 `DamageEffect`，表现为毒伤。
  - 回合开始：触发 `HealEffect`，表现为持续恢复。
  - 状态存在期间：提供 `statModifiers`，表现为 buff/debuff。
- 这样持续恢复、持续毒伤、持续属性修正都使用同一套 Status 概念，避免把持续逻辑分散到多个技能里。

## 4. 技能分类

技能按功能标签分类，而不是绑定固定职业树：

| 类型 | 说明 | AI 用途 |
|---|---|---|
| `Damage` | 单体或范围伤害 | 进攻型敌人优先 |
| `Heal` | 回复友方 HP | 治疗型敌人在友方低血量时优先 |
| `Buff` | 提高友方威胁或生存 | 支援型敌人优先 |
| `Debuff` | 降低玩家伤害、防御或移动 | 控制/支援敌人使用 |
| `Control` | 限制行动或改变站位 | 后续扩展，MVP 谨慎使用 |
| `Protect` | 护盾、防御提高、替关键单位承压 | Boss 战或支援怪使用 |
| `Mobility` | 位移、冲刺、拉近/拉远 | 依赖地形时使用 |

## 4.5 游戏 Tag 系统

Tag 是贯穿整个游戏的通用条件语言，用于连接技能、Effect、Status、AI 决策、关卡脚本和事件通知。完整规格见 `09_GAMEPLAY_TAG_SPEC.md`。Tag 是后续系统设计的第一原则：任何新的条件、事件或跨系统语义，第一时间都要考虑是否能用 Tag 表达。

Tag 格式：
- 使用点分层级，例如 `Element.Fire.Burning`、`State.Poisoned`、`Faction.Undead`。
- Tag 可以完整匹配，也可以按层级前缀匹配。
- 例如 `Element` 可以匹配所有元素类 Tag，`Element.Fire` 可以匹配所有火焰类 Tag，`Element.Fire.Burning` 只匹配燃烧本身。

使用场景：
- 技能可以给目标添加 Tag，例如 `State.Burning`、`State.Marked`。
- 技能或 Effect 可以移除 Tag，例如移除 `State.Burning`。
- 技能可以检查目标 Tag 来决定是否生效，例如拥有 `State.Immune.Fire` 时，火焰技能无效。
- Status 和 Effect 应尽量关联一个或多个 Tag，便于查询角色身上有哪些效果或状态。
- Effect 必须至少配置一个 Tag；Status 也应配置表示自身状态的 Tag。
- AI 可以读取目标 Tag 进行决策，例如优先攻击 `State.Marked` 的目标，或避免对 `State.Immune.Poison` 的目标使用毒系技能。
- 关卡脚本和 Scripted Tactic 可以通过 Tag 判断条件。

实现约定：
- Tag 本身是数据，不应写死在某个技能逻辑里。
- 第一阶段可用字符串表达 Tag，但应集中封装匹配逻辑，避免各系统各自写 `StartsWith`。
- 代码中任何传递游戏语义事件的操作，都应优先携带 Tag / TagSet。
- 新增布尔字段、专用 enum、专用事件类型前，应先判断是否能用 Tag 表达。
- Tag 不替代强类型数值和对象引用；例如伤害值、HP、AP、世界坐标仍然是强类型数据，Tag 用来标记它们的玩法语义。
- 匹配模式至少支持：
  - Exact：完整匹配。
  - Prefix：层级前缀匹配。
- 单位运行时需要能查询当前 Tag，并能追踪 Tag 来源，例如来自 Status、装备、地形、剧情或临时战术。
- 当 Tag 来自 Status 时，Status 结束应自动移除对应 Tag。

## 5. 数据载体

```csharp
[CreateAssetMenu(menuName = "Chess/SkillDefinition")]
public class SkillDefinition : ScriptableObject { /* 见 §1 字段 */ }
```
存放位置：`Assets/Data/Skills/`。

`SkillDefinition` 只保存静态配置，并显式引用一个 `SkillAbility`。每个单位的当前冷却、已施放次数、临时禁用、状态授予技能等运行时状态应放在 `AbilitySystemComponent` 的 `AbilitySpec` / `ActiveSkillEffect` 中，不写回 ScriptableObject。

Effect 配置资产存放在 `Assets/Data/Effects/`。Effect 资产同样只保存静态配置，允许多个技能共享，例如多个物理攻击技能复用同一个固定伤害或同类型伤害 Effect 模板。Effect Function 资产存放在 `Assets/Data/EffectFunctions/`，函数资产只保存函数专属参数和代码行为引用。

### 5.1 AbilitySystemComponent 与目标

- `AbilitySystemComponent` 是运行时组件，挂在能够释放技能或能够被技能影响的对象上，例如玩家、敌人、木桶、墙壁或后续特殊机关。
- 玩家输入、敌方 AI、脚本化战术都应向对象自身的 ASC 提交施放请求，而不是各自实现技能流程。
- 技能目标不应限定为敌人或玩家。木桶、墙壁、机关等可交互物体也可以成为技能目标。
- 不额外引入 `ISkillTarget` / `SkillTarget`。目标合法性统一由目标对象自身的 ASC 判断。
- 只要对象挂载 ASC，它就默认进入技能系统，可以被技能查询和影响。
- ASC 应提供最小配置来声明该对象承受技能效果的能力，例如：
  - 是否可被伤害。
  - 是否可被治疗。
  - 是否可添加 Status。
  - 是否属于玩家、敌方、中立或环境目标。
  - 是否阻挡、可破坏或可互动。
- 第一阶段可以先让玩家和敌人的 ASC 承担释放与受击能力；后续扩展木桶、墙壁等环境目标时，只需要给对象挂载并配置 ASC。

## 6. 释放流程

玩家和 AI 共用同一套技能执行流程：

1. 选择施法者。
2. 枚举可选技能。
3. 选择目标或地面点。
4. 校验 AP、冷却、目标类型、范围、路径/视线。
5. 如果目标不在范围内，允许先移动到可施放位置，再释放技能。
6. 扣除移动 AP 与技能 AP。
7. 执行效果并更新状态/冷却。
8. 若技能配置 `endsTurnAfterCast = true`，结束该单位回合。

技能 AP 消耗永远来自技能自身配置，不使用全局固定表。

默认单目标主动技能的事务顺序：

1. `AbilitySystemComponent.CanExecute` 校验施法者存活、目标存活、目标能力、`SkillDefinition` 元数据条件、Tag 条件。
2. `SkillAbility` 计算 Costs 和 Cooldowns；任一失败则技能失败，且不应用任何 Cost、Cooldown 或普通 Effect。
3. 应用 Costs；如果资源扣除失败，技能失败，且不应用 Cooldown 或普通 Effect。
4. 应用 Cooldowns。
5. 计算并应用普通 Effects；普通 Effect 失败只跳过该 Effect，除非后续引入 `FailurePolicy`。
6. 处理 `endsTurnAfterCast`。

CR-0002 Phase 2 之后，移动也属于技能流程：玩家点击地面本质上是当前激活的 `basic_move` 解释 `Input.Target.Ground` 输入请求。输入层只能发出 Tag 化输入请求，不能直接调用单位移动 API 或构造移动技能上下文绕过 ASC。

### 6.1 主动攻击释放方式（待后续确认）

当前阶段玩家输入只要求支持 `basic_move` 地面移动，不要求鼠标直接点击敌方单位触发 `basic_attack`。主动攻击技能如何释放（点击敌人、技能栏选择、快捷键或其他交互）待 AI 框架大体跑通后继续设计。

无论后续采用哪种交互，攻击技能执行仍应满足以下原则：

1. 读取当前角色自身 ASC 可用技能中配置的攻击技能，例如 `basic_attack`。
2. 使用技能自身 `range` 判断是否已经在技能距离内。
3. 若已经在范围内，并且 AP 足够支付技能 `Costs` 中的 `SpendAPEffect`，直接释放技能。
4. 若不在范围内，技能计划/AI 候选系统可以计算能否通过移动技能进入 `skill.range`。
5. 若 AP 足够支付移动技能的 `Costs` + 目标技能的 `Costs`，则执行“移动技能 -> 等待移动完成 -> 重新校验 -> 攻击技能”。
6. 若 AP 不足以抵达攻击距离，是否只移动靠近由玩家交互规则或 AI 候选评分决定。

这个规则应被敌方 AI 复用：AI 选择攻击候选时，同样依据技能范围、移动 AP 与技能 AP 判断本回合能否攻击；是否退化为靠近目标由 AI 候选评分决定。

### 6.2 冷却规则

- 技能不会消耗 AP 之外的其他资源。
- AP 消耗通过 Ability 的 `Costs` 槽位配置 `SpendAPEffect`，不再通过 `SkillDefinition.apCost` 字段表达。
- 特定技能可以通过 Ability 的 `Cooldowns` 槽位配置 Persistent `SkillEffect`，用于避免强力技能被连续使用。
- 冷却回合数不允许小数。
- 战斗中，`Cooldown` Effect `DurationRounds = 1` 表示使用后需要等待 1 个回合单位的冷却。
- 探索 / 非战斗实时状态下，世界实时更新，技能冷却按秒刷新。
- 暂定 `1 回合 = 6 秒`，因此探索状态下 `DurationRounds = 1` 等价于冷却 6 秒。
- 运行时需要维护每个单位自己的技能冷却状态；冷却状态不能写回 `SkillDefinition` 资产。
- 同一个技能配置在战斗和探索中共用同一个冷却配置，只是刷新方式不同。
- 冷却 Effect 使用 `TagOnlyEffectFunction` 或等价空行为 Function，`GrantedTags` 配置 `Cooldown.{skillId}`。Cooldown 槽位可以在执行前检查 GrantedTags 是否已存在，但不得绕过 `SkillEffect.Compute` 和目标映射规则。

## 7. MVP 技能池草案

第一阶段不要做技能树，只做能验证 AI 配合的少量技能。

| ID | 名称 | AP | 范围 | 类型 | 效果 |
|---|---|---:|---:|---|---|
| `basic_attack` | 基础攻击 | 1 | 1.5m | `Damage` | 对单体敌人造成固定伤害 |
| `guarding_shout` | 战吼 | 2 | 6m | `Buff` / `Protect` | 友方单体攻击或防御提高 1-2 回合 |
| `minor_heal` | 小治疗 | 2 | 6m | `Heal` | 回复友方单体少量 HP |
| `power_strike` | 重击 | 3 | 1.5m | `Damage` | 造成高于基础攻击的单体伤害 |
| `crippling_hex` | 虚弱诅咒 | 2 | 6m | `Debuff` | 降低玩家攻击或移动 1 回合 |

这些技能足够验证：

- 进攻型敌人会追击和攻击。
- 支援型敌人会在开场或关键时刻 buff 友军。
- 治疗型敌人会在友方低血量时优先治疗。
- AI 能在进攻、治疗、支援之间做评分选择。

## 8. 技能列表

正式技能列表需要在具体角色/怪物设计时继续填入：

| ID | 名称 | AP | 范围 | 效果 | 状态 |
|---|---|---:|---:|---|---|
| _待补_ | | | | | |

## 9. AI 使用规则

- AI 不直接理解技能效果代码，而是优先读取 `aiTags`、`targetType`、`range`、Ability 的 `Costs` / `Cooldowns` 与 `aiBaseWeight`。
- 治疗、buff、保护、控制等行为需要通过 `aiTags` 暴露给 `AIProfile`，否则 AI 只能把技能当作普通可用技能处理。
- 若技能有复杂条件（例如只能对中毒目标使用），应在技能可用性检查中返回不可用原因，供 AI 过滤候选行动和 debug 输出。
- `aiBaseWeight` 只提供技能自身的默认吸引力；最终选择仍由 `AIProfile`、目标状态、距离、AP 效率和脚本化战术共同决定。

## 10. 第一阶段实现范围

第一版技能系统只实现：

1. `SkillDefinition` 数据结构。
2. 统一 `EffectDefinition` 数据结构 + 预定义静态 `EffectFunction` 选择。
3. `ModifyAttribute`，用于完成 `basic_attack` 纵切片。
4. `ModifyAttribute` 治疗方向与 `AddStatus` 函数的扩展位置，暂不实现完整行为。
5. 基础冷却与 AP 校验。
6. 单体目标和自身目标。
7. 敌方 AI 能读取 `aiTags` 与 `aiBaseWeight`。

暂不实现：

- 技能树。
- 装备提供技能。
- 复杂连锁反应。
- 大量 VFX / 动画表现。
- 会改变先攻顺序的状态。
- 强控制类状态（眩晕、魅惑等）。

## 11. 待决问题（参见 `OPEN_QUESTIONS.md`）

- 技能是否消耗"法力值"等第二资源，还是只消耗 AP？
- 技能命中是否需要骰子检定（D&D 式 attack roll）？
- 暴击 / 闪避机制？

## 12. AttributeSet — Tag 驱动的属性系统

> 2026-05-10 重构：`ICombatUnit` 上帝接口被拆解为 `AttributeSet`（数据层）、`MovementController`（移动层）和 `CombatRoundManager`（回合生命周期）。

### 12.1 设计目标

技能系统不应依赖任何角色类型（`Player1Controller`、`EnemyController`）。任何挂载 `AttributeSet` + ASC 的 GameObject 都是合法的技能目标。

### 12.2 组件架构

```
GameObject
├── AttributeSetDef (SO)          ← 属性模板定义，不存运行时状态
├── AttributeSet (MB)             ← 运行时数据容器，Tag → float
├── MovementController (MB)       ← NavMeshAgent 移动
├── AbilitySystemComponent (MB)   ← 技能执行与运行时状态
├── (可选) EnemyController / Player1Controller  ← 视觉、AI、输入
```

### 12.3 AttributeSetDef (ScriptableObject)

属性定义资产。每条属性包含：
- `Tag`（GameplayTagRef）：属性标识，如 `Attribute.HP`、`Attribute.AP`
- `BaseValue`（float）：初始值
- `MaxValue`（float）：上限（0 = 无上限）

资产路径：`Assets/Data/Attributes/`。

### 12.4 AttributeSet (MonoBehaviour)

运行时属性容器，核心 API：

| 方法 | 说明 |
|------|------|
| `Get(tag)` | 读取当前值 |
| `Set(tag, value)` | 设置当前值（clamp 到 [0, MaxValue]） |
| `GetMax(tag)` | 读取上限 |
| `SetMax(tag, max)` | 设置上限（重新 clamp 当前值） |
| `SetToMax(tag)` | 设为最大值（AP 回满） |
| `Modify(tag, delta)` | 增加/减少（clamp，触发事件） |
| `TrySpend(tag, amount)` | 扣减（检查充足后扣减） |
| `IsAlive` | `Get("Attribute.HP") > 0` |

事件：
- `AttributeChanged(tag, oldValue, newValue)` — 任意属性变化
- `AttributeDepleted(tag)` — 属性归零（HP 归零时触发死亡逻辑）

Faction 自动同步：`AttributeSet.Awake` 将 `Faction` enum 同步到 `GameplayTagComponent` 为 `Faction.Player` / `Faction.Enemy`。

### 12.5 MovementController (MonoBehaviour)

统一的 NavMeshAgent 移动包装器，替代 EnemyController 和 Player1Controller 中重复的移动逻辑。

| 成员 | 说明 |
|------|------|
| `TryMove(path)` / `TryStartMove(path)` | 开始移动 |
| `StopMovement()` | 停止移动 |
| `IsMoving` | 是否正在移动 |
| `RemainingMoveDistance` | 剩余可移动距离（AP × MoveSpeed - unpaid） |
| `PreviewMovementApCost(length)` | 预估路径 AP 消耗 |
| `ResetUnpaidDistance()` | 回合切换时重置 |

事件：`MovementStarted`、`MovementStopped`、`ApDeducted`。

### 12.6 技能系统如何访问属性

```csharp
// EffectFunction 不再调用 ICombatUnit.TakeDamage，而是直接 Modify Tag
ModifyAttributeFunction.Apply(DamageEffect) → ctx.Target.GetComponent<AttributeSet>().Modify("Attribute.HP", -amount)
ModifyAttributeFunction.Apply(HealEffect)   → attr.Modify("Attribute.HP", +amount)

// SpendAPEffect 不再调用 TrySpendAP，而是 TrySpend Tag
SpendAPFunction.Apply(SpendAPEffect) → casterAttr.TrySpend("Attribute.AP", result.Amount)

// 主动移动不再建 MoveEffect；由 GroundMoveAbility 直接通过 MovementController 执行
GroundMoveAbility.Execute(ctx) → movement.TryStartMove(path)
```

### 12.7 Well-Known Tags

常用属性 Tag 常量（`WellKnownAttributeTags`）：

| 常量 | Tag 字符串 |
|------|-----------|
| `HP` | `"Attribute.HP"` |
| `AP` | `"Attribute.AP"` |
| `Initiative` | `"Attribute.Initiative"` |
| `MoveSpeed` | `"Attribute.MoveSpeed"` |

### 12.8 与旧 ICombatUnit 的对比

| ICombatUnit | AttributeSet |
|---|---|
| 上帝接口，19 成员 | 单一职责：数据容器 |
| 角色类型必须实现接口 | 任何 GameObject 挂载即可 |
| TakeDamage/Heal 方法 | `Modify("Attribute.HP", delta)` |
| TrySpendAP(int) | `TrySpend("Attribute.AP", amount)` |
| 硬编码属性名 | Tag 驱动，新增属性只需加 Tag |
| EnemyController/Player1Controller 重复逻辑 | MovementController 统一实现 |

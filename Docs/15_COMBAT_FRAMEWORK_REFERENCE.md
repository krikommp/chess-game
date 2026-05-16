# 15 - 战斗框架参考

> 2026-05-11 整理，2026-05-12 更新。当前 `refactor/combat-round-enemy-ai` 分支的完整战斗框架结构。

## 全局数据流

```
                    ┌─────────────────────┐
                    │  CombatRoundManager  │  ← 唯一全局回合状态机
                    │  (1个，挂Systems上)   │
                    └──────┬──────────────┘
                           │ 广播事件: UnitTurnStarted(unit)
                           │
              ┌────────────┴────────────┐
              ▼                         ▼
     ┌────────────────┐       ┌──────────────────────┐
     │ UnitTurnHandler │       │ Future AISystem       │
     │ (玩家侧调度)     │       │ (暂未实现)             │
     └───────┬────────┘       └──────────┬───────────┘
             │ 选择单位、转发输入       │ 后续生成/执行 AI 候选
             ▼                         ▼
     ┌─────────────────────────────────────────┐
     │      AbilitySystemComponent (ASC)       │  ← 每个单位一个
     │  AbilitySpec → 校验 → 扣AP → Effect/状态 │
     └────────────────┬────────────────────────┘
                      │ 读取/写入
         ┌────────────┼────────────┐
         ▼            ▼            ▼
   AttributeSet  MovementCtrl  GameplayTagComponent
   (HP/AP/属性)   (NavMesh移动)  (Tag容器)
```

---

## 第一层：系统对象组件

挂在 `Systems` 对象（或 `[CombatRoundManager]`）上，全局各一个：

| 组件 | 作用 |
|------|------|
| **CombatRoundManager** | 回合状态机。`FindObjectsOfType<CombatUnit>()` 收集单位 → 按先攻排序 → 维护可控块 → 广播 `RoundStarted` / `UnitTurnStarted` / `UnitTurnEnded` / `CombatEnded`。自身不含任何游戏逻辑（AP/技能/冷却/移动/AI/输入/相机都不在它内部） |
| **UnitTurnHandler** | 玩家回合调度。订阅 `UnitTurnStarted`，通过 `GameplayTagComponent.HasTag(Control.Human)` 筛选 → 选中单位 → 相机聚焦 → 激活 basic_move → 转发点击输入给 ASC。Update 中处理数字键(1-4)切换单位、Space 结束回合 |
| **Enemy AI System** | 暂未实现。旧 `EnemyTurnRunner` 硬编码逻辑已删除；后续通过 AIActionCandidate / AIProfile 生成候选，并统一交给 ASC 执行 |
| **InputController** | 纯输入层。鼠标 Raycast → 读点击对象的 `GameplayTagComponent`（Control.Human → k_TargetHuman, Control.AI → k_TargetAI, 地面 → k_TargetGround）→ 发出 `SkillInputRequest` |
| **CameraController** | 通常挂在 `Main Camera` 上。45° 固定角相机，支持 WASD/中键拖拽平移、滚轮缩放、`FocusOn(Transform)` 平滑跟随 |

---

## 第二层：单位组件栈

### 核心 5 件套（玩家和敌人都必须有）

| 组件 | 类型 | 作用 |
|------|------|------|
| **CombatUnit** | MonoBehaviour（空） | 纯标记。让 `FindObjectsOfType<CombatUnit>()` 发现参战单位，解耦具体控制器类型 |
| **AttributeSet** | MonoBehaviour | 运行时属性容器。`Get(HP)` / `Modify(HP, -20)` / `TrySpend(AP, 1)` / `Faction` / `IsAlive`。通过 `AttributeSetDef` SO 初始化。`Awake()` 中自动同步 Faction Tag 到 GameplayTagComponent |
| **MovementController** | MonoBehaviour `[RequireComponent(NavMeshAgent, AttributeSet)]` | NavMeshAgent 包装器。`TryStartMove(path)` / `StopMovement()` / `IsMoving`。移动 AP 由技能 Cost 结算，MovementController 保持纯移动职责 |
| **AbilitySystemComponent** | MonoBehaviour | 技能执行统一入口。持有 `AbilitySpec` 列表、当前激活技能、授予技能、持续 `ActiveSkillEffect`、冷却 Tag、被动 Ability 触发。`CanExecute(ctx)` → `Execute(ctx)` → 调 `SkillAbility.Execute()` |
| **GameplayTagComponent** | MonoBehaviour | 运行时 Tag 容器。`AddTag(tag, source)` / `HasTag(tag, mode)` / `RemoveAllTagsFromSource(source)`。技能条件、AI 决策、Effect 都查它 |

### 控制权标识（通过 Tag，非组件）

> **设计决议 (2026-05-11):** 单位由谁操作通过 `GameplayTagComponent` 上的 Tag 表达，不通过组件类型区分。
> `Control.Human` = 玩家操作，`Control.AI` = AI 操作。天然兼容心控、Charm、托管自动战斗等控制权转移场景。
> 当前暂不设计阵营系统。

### 物理组件

| 组件 | 说明 |
|------|------|
| **NavMeshAgent** | Unity 原生。MovementController `[RequireComponent]`，每个可移动单位必须挂 |

### 单位完整组件清单

```
任何参战单位:
  CombatUnit            (标记，让 CombatRoundManager 发现)
  GameplayTagComponent  (Control.Human / Control.AI + Faction.Player / Faction.Enemy 等)
  AttributeSet          (挂 AttributeSetDef SO)
  MovementController    (自动添加 NavMeshAgent)
  AbilitySystemComponent(挂 basic_move 等 SkillDefinition / AbilitySpec)
  (后续) UnitVisualController  (纯视觉：颜色切换、闪红、死亡 VFX)
  (后续) AIUnitConfig          (仅 AI 单位：挂 AIProfile 引用)
```

### 已删除的历史组件

| 组件 | 删除日期 | 替代方案 |
|------|---------|---------|
| **Player1Controller** | 2026-05-12 | Tag `Control.Human` + ASC。视觉状态（颜色切换）→ `UnitVisualController`(待实现)；PartySlot → UnitTurnHandler 管理 |
| **EnemyController** | 2026-05-12 | Tag `Control.AI` + ASC。死亡处理 → `sys_on_death` 系统技能；FlashTurn → `UnitVisualController`(待实现)；AIProfile → `AIUnitConfig`(待实现) |

---

## 第三层：ScriptableObject 资产

| 资产 | 路径 | 用途 |
|------|------|------|
| **SkillDefinition / SkillMetadata** | `Assets/Data/Skills/` | 技能静态配置：id/displayName/description、targetType、range、areaShape、requiresLineOfSight、endsTurnAfterCast、skillTags、aiTags、aiBaseWeight、表现引用、`SkillAbility` 引用。给 UI、AI、预览和配置校验读取，不保存运行时状态 |
| **SkillAbility** (抽象) | `Assets/Data/SkillAbilities/` | 技能流程资产。实现 `abstract Execute(context)`，编排 Costs/Cooldowns/Effects、移动或特殊逻辑。必须按无状态共享 SO 设计，不能保存当前施法者、目标、冷却或释放结果 |
| **SkillEffect** (sealed) | `Assets/Data/Effects/` | 效果数据。**Function** (SkillEffectFunction SO)、目标映射、Duration、StackRule、Tick 设置、Tag 互作、Stat Modifiers、Granted Abilities。接口：`Compute(ctx)→SkillEffectResult`，生命周期由 ASC 分成 OnApply/OnTick/OnRemove |
| **AttributeSetDef** | `Assets/Data/` | 属性模板：定义单位有哪些属性(HP/AP/Initiative/MoveSpeed)，初始值和最大值 |
| **AIProfile** | `Assets/Data/` | AI 行为档案：Role(Aggressive/Support/Healer)、healHpThreshold、skillTagWeights、targetTagWeights、statusTagWeights（数据结构已有，AI 执行系统暂未实现） |
| **TagRegistry** | `Assets/Data/` | 全局 Tag 注册表：所有合法 Tag 的索引+描述，供编辑器和校验工具使用 |

### Skill / Ability 设计原则 (2026-05-16)

1. **一个可展示/可评分技能 = 一个 SkillDefinition**。SkillDefinition 是 UI、AI、预览、配置校验的读取入口。
2. **一个执行流程 = 一个 SkillAbility**。多个 SkillDefinition 可以复用同一个 Ability 流程，例如多个单体攻击技能复用 `SimpleTargetAbility`，通过不同 Costs/Effects/Metadata 区分。
3. **Targeting/Range/AI 语义归 SkillDefinition**。ASC 通用校验优先读取 `targetType/range/areaShape/requiresLineOfSight/skillTags/aiTags/aiBaseWeight`。
4. **流程权限归 Ability / Tag 条件**。例如 `GroundMoveAbility` 可以被 `State.Rooted` 阻断。
5. **资源支付归 Cost Effect**。例如 `SpendAPEffect.BlockedTags = [State.APBlocked]`，并通过 Effect TargetMapping 检查 Caster。
6. **运行时状态归 AbilitySpec / ActiveSkillEffect**。任何 SO 资产不得记录当前施法者、当前目标、冷却剩余、叠层或本次释放缓存。

### AbilitySpec

ASC 的可用技能列表已从裸 `SkillAbility[]` 切换为 `SkillDefinition[] -> AbilitySpec[]`：

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

第一阶段 `Level = 1`，暂不引入充能、等级成长和装备来源复杂规则。状态授予技能、装备授予技能、临时禁用等都通过 `AbilitySpec` 或其 `GrantSource` 表达。

---

## 第四层：纯工具/数据类

| 类 | 类型 | 作用 |
|----|------|------|
| **NavMeshService** | MonoBehaviour singleton | NavMesh 路径计算、范围判断、AP 消耗估算、路径裁剪。合并了原 CombatMovementResolver + PathCostCalculator |
| **SkillExecutionContext** | struct | 打包一次技能释放的上下文：Caster/Target/Skill/Path/InputRequest。工厂方法：`ForTarget()` / `ForGroundPoint()` / `ForInput()` |
| **SkillCastResult** | struct | 技能执行结果：`IsSuccess` / `Failure`(ESkillCastFailure) / `FailureMessage` |
| **SkillEffectContext** | struct | Effect 上下文：Caster/Target/CasterExecutor/TargetExecutor/TargetPosition。构建时必须应用 `ESkillEffectTarget` 目标映射 |
| **SkillPlan** | struct | 移动+技能组合计划：MovementSkill/PrimarySkill/PrimaryTarget/MovePath/MoveApCost/PrimaryApCost/TotalApCost/CanExecutePrimaryThisTurn/IsMovementOnly。已定义但基本未用 |
| **ActiveSkillEffect** | class `[Serializable]` | 运行时持续效果实例：Definition/Source/Owner/RemainingRounds/StackCount/已应用属性修正快照。该实例本身作为 GameplayTag source handle |
| **SkillInputRequest** | readonly struct | Tag 化输入事件：SignalTag/TargetTag/TargetObject/WorldPosition。`IsSignal(tag)` / `IsTarget(tag)` |
| **GameplayTag** | struct `IEquatable` | 核心 Tag 值对象：FNV-1a hash(大小写不敏感) + 字符串。前缀匹配。隐式转换 `string → GameplayTag` |
| **GameplayTagSet** | sealed class | Tag 运行时容器：多源追踪(同一 Tag 被多个 source 添加时只存一份，所有 source 移除后才真删除) |
| **TagQuery** | struct `[Serializable]` | 条件查询：RequiredAll/RequiredAny/BlockedAny + MatchMode(Exact/Prefix)。技能条件和 AI 决策使用 |

### 关键枚举

| 枚举 | 值 |
|------|----|
| **EFaction** | Player, Enemy |
| **ESkillCastFailure** | None/CasterDead/TargetDead/TargetInvalid/InsufficientAp/OnCooldown/OutOfRange/TargetCapabilityBlocked/TagConditionFailed/EffectApplicationFailed |
| **ETargetCapability** | None/Damageable(1)/Healable(2)/Statusable(4)/Interactable(8)/Destructible(16)/Movable(32) |
| **EAIRole** | Aggressive, Support, Healer |

---

## 当前问题速查

| ID | 问题 | 严重级别 |
|----|------|---------|
| IS-0019 | 场景未迁移：单位缺 CombatUnit，系统对象缺 UnitTurnHandler / 未来 AI System | P0 |
| IS-0020 | 回合开始不恢复 AP、不重置移动预算（ASC.OnRoundStart 只推进冷却） | P0 |
| IS-0001 | Status 系统完全缺失（AddStatusEffectDefinition 只有占位 Apply） | P0 |
| IS-0002 | AI Action Candidate 框架未实现（旧 EnemyTurnRunner 已删除，敌方暂不自动行动） | P0 |
| IS-0003 | CombatRoundManager 虽已精简但仍包含 CollectUnits/BuildTurnOrder/CheckVictory 等可拆逻辑 | P0 |
| IS-0004 | GroundMoveAbility.DefaultInstance 运行时 CreateInstance（破坏 SO 资产约定） | P0 |
| IS-0005 | ~~Player1Controller/EnemyController pass-through~~ → ✅ 已删除 (2026-05-12) | ~~P1~~ 已解决 |
| IS-0007 | ASC CollectTags 重复 Faction 同步（AttributeSet.Awake 已同步一次） | P1 |
| IS-0008 | EnemySpawner 使用 Testing_ API（Build 后无效），且缺 CombatUnit/GameplayTagComponent | P1 |
| IS-0017 | ~~Player1Controller.OnAttributeDepleted 为空~~ → ✅ 已删除 (2026-05-12) | ~~P3~~ 已解决 |
| IS-0021 | UnitTurnHandler 点击选择未校验 ControllableUnits（可能提前控制尚不该行动的玩家） | P1 |

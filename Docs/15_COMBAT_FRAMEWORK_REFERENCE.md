# 15 - 战斗框架参考

> 2026-05-11 整理。当前 `refactor/combat-round-enemy-ai` 分支的完整战斗框架结构。

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
     ┌────────────────┐       ┌─────────────────┐
     │ UnitTurnHandler │       │ EnemyTurnRunner  │
     │ (玩家侧调度)     │       │ (敌方AI调度)      │
     └───────┬────────┘       └────────┬────────┘
             │ 选择单位、转发输入       │ 选技能、移动、攻击
             ▼                         ▼
     ┌─────────────────────────────────────────┐
     │            SkillExecutor                │  ← 每个单位一个
     │  校验 → 扣AP → 移动 → 执行Effect → 冷却 │
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
| **UnitTurnHandler** | 玩家回合调度。订阅 `UnitTurnStarted`，通过 `GameplayTagComponent.HasTag(Control.Human)` 筛选 → 选中单位 → 相机聚焦 → 激活 basic_move → 转发点击输入给 SkillExecutor。Update 中处理数字键(1-4)切换单位、Space 结束回合 |
| **EnemyTurnRunner** | 敌方 AI 回合调度。订阅 `UnitTurnStarted`，通过 `GameplayTagComponent.HasTag(Control.AI)` 筛选 → 启动 AI 评估 → 通过 SkillExecutor 执行最优候选。当前为硬编码 MVP 逻辑（找最近玩家 → 移动 → 攻击），未使用 AIProfile |
| **InputController** | 纯输入层。鼠标 Raycast → 读点击对象的 `GameplayTagComponent`（Control.Human → k_TargetHuman, Control.AI → k_TargetAI, 地面 → k_TargetGround）→ 发出 `SkillInputRequest` |
| **CameraController** | 通常挂在 `Main Camera` 上。45° 固定角相机，支持 WASD/中键拖拽平移、滚轮缩放、`FocusOn(Transform)` 平滑跟随 |

---

## 第二层：单位组件栈

### 核心 5 件套（玩家和敌人都必须有）

| 组件 | 类型 | 作用 |
|------|------|------|
| **CombatUnit** | MonoBehaviour（空） | 纯标记。让 `FindObjectsOfType<CombatUnit>()` 发现参战单位，解耦具体控制器类型 |
| **AttributeSet** | MonoBehaviour | 运行时属性容器。`Get(HP)` / `Modify(HP, -20)` / `TrySpend(AP, 1)` / `Faction` / `IsAlive`。通过 `AttributeSetDef` SO 初始化。`Awake()` 中自动同步 Faction Tag 到 GameplayTagComponent |
| **MovementController** | MonoBehaviour `[RequireComponent(NavMeshAgent, AttributeSet)]` | NavMeshAgent 包装器。`TryStartMove(path)` → 每帧按实际移动距离扣 AP。`IsMoving` / `ResetUnpaidDistance()` / `RemainingMoveDistance` |
| **SkillExecutor** | MonoBehaviour | 技能执行统一入口。`CanExecute(ctx)` → `Execute(ctx)` → 校验（AP/冷却/范围/Tag/阵营）→ 扣 AP → 调 SkillAbility → 执行 Effects → 记录冷却。管理 `AvailableSkills`、冷却追踪、持续 Effect 每回合 tick |
| **GameplayTagComponent** | MonoBehaviour | 运行时 Tag 容器。`AddTag(tag, source)` / `HasTag(tag, mode)` / `RemoveAllTagsFromSource(source)`。技能条件、AI 决策、Effect 都查它 |

### 控制权标识（通过 Tag，非组件）

> **设计决议 (2026-05-11):** 单位由谁操作通过 `GameplayTagComponent` 上的 Tag 表达，不通过组件类型区分。
> `Control.Human` = 玩家操作，`Control.AI` = AI 操作。天然兼容心控、Charm、托管自动战斗等控制权转移场景。
> 当前暂不设计阵营系统。

### 历史组件（待删除）

| 组件 | 状态 | 处理方案 |
|------|------|---------|
| **Player1Controller** | 仍存在于代码中 | 删除。视觉状态（颜色切换）→ `UnitVisualController`；PartySlot → 由 UnitTurnHandler 管理；其余 pass-through 属性直接访问 AttributeSet/MovementController |
| **EnemyController** | 仍存在于代码中 | 删除。死亡处理 → `sys_on_death` 系统技能；AIProfile → `AIUnitConfig` 组件或 EnemyTurnRunner 配置表；FlashTurn → `UnitVisualController`；其余 pass-through 属性直接访问 AttributeSet/MovementController |

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
  SkillExecutor         (挂 basic_move 等 SkillDefinition)
  (后续) UnitVisualController  (纯视觉：颜色切换、闪红、死亡 VFX)
  (后续) AIUnitConfig          (仅 AI 单位：挂 AIProfile 引用)
```

> **当前临时状态：** Player1Controller / EnemyController 仍存在于代码中但已决议删除。
> 在新组件（UnitVisualController / AIUnitConfig）实现前，它们继续承担视觉和配置功能。

---

## 第三层：ScriptableObject 资产

| 资产 | 路径 | 用途 |
|------|------|------|
| **SkillDefinition** | `Assets/Data/Skills/` | 技能数据：id/apCost/cooldown/range/targetType/effects[]/skillTags/aiTags/aiBaseWeight/requiredTargetTags/blockedTargetTags |
| **EffectDefinition** (抽象) | `Assets/Data/Effects/` | 效果基类。**Tag 互作：** IdentityTags/GrantTags/RemoveTags/RequiredTags/BlockedTags。**生命周期：** DurationRounds(isPersistent)/StackRule/TickPerRound。**数据：** StatModifiers/GrantedAbilities。**接口：** Compute(ctx)→Result / Apply(ctx,result)。派生：`DamageEffectDefinition` / `HealEffectDefinition` / `AddStatusEffectDefinition` / `MoveEffectDefinition` / `SpendAPEffect` / `SetCooldownEffect` / `RestoreAttributeEffect` 等 |
| **SkillAbility** (抽象) | `Assets/Data/` | 技能行为基类：`CanApply(ctx)` / `Apply(ctx)` / `HandleInput(ctx, input)`。当前唯一实现：`GroundMoveAbility`（地面移动的路径预览+执行） |
| **AttributeSetDef** | `Assets/Data/` | 属性模板：定义单位有哪些属性(HP/AP/Initiative/MoveSpeed)，初始值和最大值 |
| **AIProfile** | `Assets/Data/` | AI 行为档案：Role(Aggressive/Support/Healer)、healHpThreshold、skillTagWeights、targetTagWeights、statusTagWeights（数据结构完整，但 EnemyTurnRunner 尚未使用） |
| **TagRegistry** | `Assets/Data/` | 全局 Tag 注册表：所有合法 Tag 的索引+描述，供编辑器和校验工具使用 |

---

## 第四层：纯工具/数据类

| 类 | 类型 | 作用 |
|----|------|------|
| **CombatMovementResolver** | static class | NavMesh 路径计算、范围判断、软占位检查、fallback 靠近点。玩家预览和 AI 共用 |
| **PathCostCalculator** | static class | 路径长度计算、AP 消耗换算（`路径距离 / MoveSpeed` = AP）、路径裁剪 |
| **SkillExecutionContext** | struct | 打包一次技能释放的上下文：Caster/Target/Skill/Path/InputRequest。工厂方法：`ForTarget()` / `ForGroundPoint()` / `ForInput()` |
| **SkillCastResult** | struct | 技能执行结果：`IsSuccess` / `Failure`(ESkillCastFailure) / `FailureMessage` |
| **EffectContext** | struct | Effect.Apply 上下文：Caster/Target/CasterExecutor/TargetExecutor/TargetPosition |
| **SkillPlan** | struct | 移动+技能组合计划：MovementSkill/PrimarySkill/PrimaryTarget/MovePath/MoveApCost/PrimaryApCost/TotalApCost/CanExecutePrimaryThisTurn/IsMovementOnly。已定义但基本未用 |
| **ActiveEffect** | class `[Serializable]` | 运行时持续效果实例：Definition(EffectDefinition)/Source(GameObject)/RemainingRounds(int) |
| **SkillInputRequest** | readonly struct | Tag 化输入事件：SignalTag/TargetTag/TargetObject/WorldPosition。`IsSignal(tag)` / `IsTarget(tag)` |
| **GameplayTag** | struct `IEquatable` | 核心 Tag 值对象：FNV-1a hash(大小写不敏感) + 字符串。前缀匹配。隐式转换 `string → GameplayTag` |
| **GameplayTagSet** | sealed class | Tag 运行时容器：多源追踪(同一 Tag 被多个 source 添加时只存一份，所有 source 移除后才真删除) |
| **TagQuery** | struct `[Serializable]` | 条件查询：RequiredAll/RequiredAny/BlockedAny + MatchMode(Exact/Prefix)。技能条件和 AI 决策使用 |

### 关键枚举

| 枚举 | 值 |
|------|----|
| **EFaction** | Player, Enemy |
| **ESkillTargetType** | Self, SingleEnemy, SingleAlly, GroundPoint, Area |
| **ESkillCastFailure** | None/CasterDead/TargetDead/TargetInvalid/InsufficientAp/OnCooldown/OutOfRange/TargetCapabilityBlocked/TagConditionFailed/EffectApplicationFailed |
| **ETargetCapability** | None/Damageable(1)/Healable(2)/Statusable(4)/Interactable(8)/Destructible(16)/Movable(32) |
| **EAIRole** | Aggressive, Support, Healer |

---

## 当前问题速查

| ID | 问题 | 严重级别 |
|----|------|---------|
| IS-0019 | 场景未迁移：单位缺 CombatUnit，系统对象缺 UnitTurnHandler/EnemyTurnRunner | P0 |
| IS-0020 | 回合开始不恢复 AP、不重置移动预算（SkillExecutor.OnRoundStart 只推进冷却） | P0 |
| IS-0001 | Status 系统完全缺失（AddStatusEffectDefinition 只有占位 Apply） | P0 |
| IS-0002 | AI Action Candidate 框架未实现（AIProfile 数据结构完整但未被 EnemyTurnRunner 使用） | P0 |
| IS-0003 | CombatRoundManager 虽已精简但仍包含 CollectUnits/BuildTurnOrder/CheckVictory 等可拆逻辑 | P0 |
| IS-0004 | GroundMoveAbility.DefaultInstance 运行时 CreateInstance（破坏 SO 资产约定） | P0 |
| IS-0005 | ~~Player1Controller/EnemyController pass-through~~ → 已决议：删除整个类，见设计决议 | ~~P1~~ 已决议 |
| IS-0007 | SkillExecutor.CollectTags 重复 Faction 同步（AttributeSet.Awake 已同步一次） | P1 |
| IS-0008 | EnemySpawner 使用 Testing_ API（Build 后无效），且缺 CombatUnit/GameplayTagComponent | P1 |
| IS-0017 | ~~Player1Controller.OnAttributeDepleted 为空~~ → 已决议：统一走 sys_on_death 系统技能 | ~~P3~~ 已决议 |
| IS-0021 | UnitTurnHandler 点击选择未校验 ControllableUnits（可能提前控制尚不该行动的玩家） | P1 |
| IS-0022 | UnitTurnHandler.cs.meta 未完整提交 | P2 |

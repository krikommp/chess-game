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
     ┌────────────────┐       ┌─────────────────┐
     │ UnitTurnHandler │       │ EnemyTurnRunner  │
     │ (玩家侧调度)     │       │ (敌方AI调度)      │
     └───────┬────────┘       └────────┬────────┘
             │ 选择单位、转发输入       │ 选技能、移动、攻击
             ▼                         ▼
     ┌─────────────────────────────────────────┐
     │       AbilitySystemComponent            │  ← 每个单位一个
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
| **UnitTurnHandler** | 玩家回合调度。订阅 `UnitTurnStarted`，通过 `GameplayTagComponent.HasTag(Control.Human)` 筛选 → 选中单位 → 相机聚焦 → 激活 basic_move → 转发点击输入给 `AbilitySystemComponent`。不特判具体 Ability，也不构造移动路径。Update 中处理数字键(1-4)切换单位、Space 结束回合 |
| **EnemyTurnRunner** | 敌方 AI 回合调度。订阅 `UnitTurnStarted`，通过 `GameplayTagComponent.HasTag(Control.AI)` 筛选 → 启动 AI 评估 → 通过 `AbilitySystemComponent` 执行最优候选。当前为硬编码 MVP 逻辑（找最近玩家 → 移动 → 攻击），未使用 AIProfile |
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
| **AbilitySystemComponent** | MonoBehaviour | 技能执行统一入口。`CanExecute(ctx)` → `Execute(ctx)` → Tag 条件校验 → 调 SkillAbility.Execute() → 由 Ability 内部处理 Costs/Cooldowns/Effects。`HandleInput(request)` 将输入交给当前激活技能的 `ISkillInputHandler`。管理 `AvailableSkills`、冷却追踪、持续 Effect 每回合 tick |
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
  AbilitySystemComponent (挂 basic_move 等 SkillAbility)
  (后续) UnitVisualController  (纯视觉：颜色切换、闪红、死亡 VFX)
  (后续) AIUnitConfig          (仅 AI 单位：挂 AIProfile 引用)
```

### 已删除的历史组件

| 组件 | 删除日期 | 替代方案 |
|------|---------|---------|
| **Player1Controller** | 2026-05-12 | Tag `Control.Human` + `AbilitySystemComponent`。视觉状态（颜色切换）→ `UnitVisualController`(待实现)；PartySlot → UnitTurnHandler 管理 |
| **EnemyController** | 2026-05-12 | Tag `Control.AI` + `AbilitySystemComponent`。死亡处理 → `sys_on_death` 系统技能；FlashTurn → `UnitVisualController`(待实现)；AIProfile → `AIUnitConfig`(待实现) |

---

## 第三层：ScriptableObject 资产

| 资产 | 路径 | 用途 |
|------|------|------|
| **SkillAbility** (抽象) | `Assets/Data/Skills/` | 技能资产基类，合并了原 SkillDefinition 和 SkillAbility。一个 ScriptableObject = 一个技能。属性分三组：**Identity** (id/displayName/description)、**Execution Slots** (costs[]/cooldowns[]/effects[])、**Tag Conditions** (requiredCasterTags[]/blockedCasterTags[]/requiredTargetTags[]/blockedTargetTags[])。子类实现 `abstract Execute(context)`。需要解释输入的技能额外实现 `ISkillInputHandler`。当前具体类：`GroundMoveAbility`（地面移动）、`SimpleTargetAbility`（单目标技能） |
| **EffectDefinition** (sealed) | `Assets/Data/Effects/` | 效果数据。**Function** (EEffectFunction 枚举决定行为)、**参数** (amount/attributeTag/cooldownSkillId 等)、**Duration** (Instant/Persistent + StackRule + TickPerRound)、**Tag 互作** (tags[]/grantedTags[]/removeTags[]/requiredTags[]/blockedTags[])、**Stat Modifiers**、**Granted Abilities**。接口：`Compute(ctx)→EffectResult` / `Apply(ctx, result)` |
| **AttributeSetDef** | `Assets/Data/` | 属性模板：定义单位有哪些属性(HP/AP/Initiative/MoveSpeed)，初始值和最大值 |
| **AIProfile** | `Assets/Data/` | AI 行为档案：Role(Aggressive/Support/Healer)、healHpThreshold、skillTagWeights、targetTagWeights、statusTagWeights（数据结构完整，但 EnemyTurnRunner 尚未使用） |
| **TagRegistry** | `Assets/Data/` | 全局 Tag 注册表：所有合法 Tag 的索引+描述，供编辑器和校验工具使用 |

### SkillAbility 设计原则 (2026-05-12)

1. **一个技能 = 一个 SkillAbility 子类资产**。没有额外的 SkillDefinition 包装层，没有 `.Ability` 间接引用。
2. **Targeting/Range/Faction 不由 SkillAbility 自己定义**，统一通过 Tag Conditions + Cost Effect 表达。例如：只对敌人使用 → `m_requiredTargetTags: [Faction.Enemy]`；射程限制 → 在 `m_costs` 里放 CheckRange Effect。
3. **Identity Tags (skillTags/aiTags) 和 AI BaseWeight 暂不纳入**，当前无 gameplay 调用方，等实际需求出现再定。
4. **m_blockedCasterTags 涵盖原 m_abilityBlockedTags 的职能**，消除冗余的 CheckAbilityTags() 步骤。

---

## 第四层：纯工具/数据类

| 类 | 类型 | 作用 |
|----|------|------|
| **NavMeshService** | MonoBehaviour singleton | NavMesh 路径计算、范围判断、AP 消耗估算、路径裁剪。合并了原 CombatMovementResolver + PathCostCalculator |
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
| IS-0005 | ~~Player1Controller/EnemyController pass-through~~ → ✅ 已删除 (2026-05-12) | ~~P1~~ 已解决 |
| IS-0007 | SkillExecutor.CollectTags 重复 Faction 同步（AttributeSet.Awake 已同步一次） | P1 |
| IS-0008 | EnemySpawner 使用 Testing_ API（Build 后无效），且缺 CombatUnit/GameplayTagComponent | P1 |
| IS-0017 | ~~Player1Controller.OnAttributeDepleted 为空~~ → ✅ 已删除 (2026-05-12) | ~~P3~~ 已解决 |
| IS-0021 | UnitTurnHandler 点击选择未校验 ControllableUnits（可能提前控制尚不该行动的玩家） | P1 |

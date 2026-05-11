# 11 - 代码框架审查 (Framework Review)

> 2026-05-10 完整代码审查。本文档描述当前代码架构的完整状态，包括已实现系统、实现质量、未完成部分和架构关系。

## 1. 总体架构概览

```
Project Root
├── Assets/Scripts/
│   ├── CameraController.cs              ← 45° 固定视角相机
│   ├── GameplayTags/                    ← Tag 基础设施层
│   │   ├── GameplayTag.cs               ← FNV-1a int-hash 值对象
│   │   ├── GameplayTagSet.cs            ← 来源追踪的 Tag 容器
│   │   ├── GameplayTagComponent.cs      ← MonoBehaviour 包装
│   │   ├── TagQuery.cs                  ← 条件查询 (RequiredAll/Any, BlockedAny)
│   │   ├── TagRegistry.cs               ← SO 全局 Tag 数据库
│   │   ├── TagMatchMode.cs              ← Exact / Prefix 枚举
│   │   ├── TagSourceType.cs             ← 来源类型枚举
│   │   └── Generated/
│   │       └── GameplayTagConstants.g.cs ← 自动生成的 Tag 常量
│   └── Combat/
│       ├── AI/
│       │   ├── AIProfile.cs             ← SO 行为档案
│       │   └── EAIRole.cs               ← Aggressive/Support/Healer
│       ├── Skills/
│       │   ├── SkillDefinition.cs       ← SO 技能配置
│       │   ├── SkillAbility.cs          ← 技能行为抽象基类
│       │   ├── GroundMoveAbility.cs     ← 地面移动能力
│       │   ├── SkillExecutor.cs         ← 技能执行统一入口
│       │   ├── SkillExecutionContext.cs ← 执行上下文
│       │   ├── SkillCastResult.cs       ← 执行结果
│       │   ├── SkillPlan.cs             ← 移动+技能复合计划
│       │   ├── EffectDefinition.cs      ← 效果抽象基类
│       │   ├── EffectContext.cs         ← 效果执行上下文
│       │   ├── DamageEffectDefinition.cs ← 伤害效果
│       │   ├── HealEffectDefinition.cs  ← 治疗效果（框架占位）
│       │   ├── AddStatusEffectDefinition.cs ← 添加状态效果（占位）
│       │   ├── MoveEffectDefinition.cs  ← 移动效果（语义标记）
│       │   ├── SkillTargetType.cs       ← 目标类型枚举
│       │   ├── AISkillTag.cs            ← AI 技能标签常量
│       │   ├── ESkillCastFailure.cs     ← 失败原因枚举
│       │   └── ETargetCapability.cs     ← 目标能力 Flags
│       ├── Debug/
│       │   └── APDebugHUD.cs            ← OnGUI 调试 HUD
│       ├── AttributeSet.cs              ← 运行时属性容器 (Tag→float)
│       ├── AttributeSetDef.cs           ← SO 属性模板
│       ├── MovementController.cs        ← 统一 NavMesh 移动包装
│       ├── CombatRoundManager.cs        ← 回合管理 / 状态机
│       ├── CombatMovementResolver.cs    ← 共享寻路/范围解析
│       ├── PathCostCalculator.cs        ← 路径长度/AP 估算/裁剪
│       ├── PathPreview.cs               ← 路径预览线渲染
│       ├── NavMeshManager.cs            ← NavMesh 配置单例
│       ├── InputController.cs           ← 纯输入 → SkillInputRequest
│       ├── SkillInputRequest.cs         ← Tag 化输入请求
│       ├── SkillInputTag.cs             ← 输入 Tag 常量
│       ├── Player1Controller.cs         ← 玩家角色
│       ├── EnemyController.cs           ← 敌方角色
│       ├── EnemySpawner.cs              ← 临时敌人生成器
│       └── EnemyTurnRunner.cs           ← MVP 敌方 AI
```

## 2. 分层架构

### 2.1 基础设施层 — GameplayTag 系统

**状态：已完整实现**

GameplayTag 系统是本项目的**第一层语义基础设施**，所有跨系统语义都通过 Tag 表达。

#### 核心数据结构

| 类/结构 | 文件 | 职责 | 状态 |
|---------|------|------|------|
| `GameplayTag` | `GameplayTags/GameplayTag.cs` | 不可变值对象，FNV-1a int-hash 比较 | ✅ |
| `GameplayTagSet` | `GameplayTags/GameplayTagSet.cs` | 运行时 Tag 容器，支持来源追踪 | ✅ |
| `GameplayTagComponent` | `GameplayTags/GameplayTagComponent.cs` | GameObject 级 TagSet 持有者 | ✅ |
| `TagQuery` | `GameplayTags/TagQuery.cs` | 条件查询器 | ✅ |
| `TagRegistry` | `GameplayTags/TagRegistry.cs` | 全局 Tag 数据库 SO | ✅ |
| `GameplayTagConstants.g.cs` | `GameplayTags/Generated/` | 代码生成 Tag 常量 | ✅ |

#### GameplayTag (readonly struct)

```
核心设计:
- 底层存储: m_value (string) + m_id (int hash)
- Hash 算法: FNV-1a, case-insensitive
- 等值比较: 仅通过 hash (Id)，不比较字符串
- 隐式转换: string → GameplayTag (自动计算 hash)
- 校验: 禁止空值、前后点、双点、空白符
- 匹配: Exact (hash 比较) / Prefix (段级前缀比较)
```

**关键实现细节：**
- `GameplayTag(string value)` 构造函数在非法输入时抛出 `ArgumentException`，调用方需先调用 `IsValid()` 或确保输入合法
- `Matches()` 的 Prefix 模式按 `.` 分割后逐段比较，不是单纯字符串前缀
- 所有 Tag 常量应通过 `GameplayTagConstants`（生成代码）或 `static readonly` 字段访问，不应散落字符串

#### GameplayTagSet (runtime container)

```
核心设计:
- 内部存储: Dictionary<GameplayTag, HashSet<object>>
- 来源追踪: 每个 Tag 维护一个来源对象集合
- 添加: Add(tag, source) — 空来源默认 → ETagSourceType.Debug
- 移除: Remove(tag, source) — 只移除指定来源; source==null → 全删
- 批量移除: RemoveAllFromSource(source) — Status 过期时清理
- 查询: Has / HasAny / HasAll 支持 Exact / Prefix
```

**当前不足：**
- 来源追踪使用的是 `object` 类型（弱类型），实际使用时传入的是字符串常量如 `"SkillExecutor.CollectTags"`。后续需要统一来源追踪的类型安全。
- `RemoveAllFromSource` 在迭代时构建 `toRemove` 列表，对于频繁操作可能产生 GC 压力。

#### TagQuery (条件查询)

```
字段:
- RequiredAll: 目标必须全部拥有
- RequiredAny: 目标至少拥有一个
- BlockedAny: 目标不能拥有任何一个
- MatchMode: Exact / Prefix

空标记: 空字符串 Tag 在 Evaluate 时被自动跳过
```

#### TagRegistry (ScriptableObject)

```
路径: Assets/Data/Tags/GameplayTagRegistry.asset (建议)
字段:
- m_entries: List<TagEntry>
  - TagEntry: Tag + DisplayName + Description
方法: TryGetEntry / IsRegistered
```

**当前不足：**
- `IsRegistered` 和 `TryGetEntry` 使用线性遍历 `List<TagEntry>`，当 Tag 数量增长时性能会成为问题。建议改用 `Dictionary`。
- 缺少废弃标记字段（`deprecated`），虽然 `TagEntry` 结构支持扩展。
- 缺少被引用位置追踪（哪些 Skill/Effect/Status 引用了该 Tag）。

### 2.2 数据层 — Attribute 系统

**状态：已完整实现**

#### 设计理念

`ICombatUnit` 上帝接口已被拆解为：
- `AttributeSet` (MonoBehaviour) — 数据容器
- `MovementController` (MonoBehaviour) — 移动层
- `SkillExecutor` (MonoBehaviour) — 技能执行层

任何挂载 `AttributeSet` + `SkillExecutor` 的 GameObject 都是合法技能目标，不依赖角色类型。

#### AttributeSet (运行时属性容器)

```
核心 API:
- Get(tag) → float          : 读取当前值
- Set(tag, value)           : 设置 (clamp到[0,Max])
- GetMax(tag) → float       : 读取上限
- SetMax(tag, max)          : 设置上限 (re-clamp当前值)
- SetToMax(tag)             : 回满 (AP回满)
- Modify(tag, delta) → float: 增加/减少 (clamp, 触发事件)
- TrySpend(tag, amount) → bool : 扣减 (先检查充足)
- IsAlive → bool            : HP > 0

事件:
- AttributeChanged(tag, oldValue, newValue)
- AttributeDepleted(tag) — tag归零时触发

Faction:
- 从 m_faction 枚举自动同步到 GameplayTagComponent 为 Faction.Player / Faction.Enemy
- OverrideFactionForTesting() 用于测试设置
```

**关键实现细节：**
- `TrySpend(amount <= 0)` 直接返回 false，防止非法扣减
- `Set` 和 `Modify` 都通过 `ClampToMax` 限制上限
- `AttributeDepleted` 只在值从正数变为 ≤0 时触发
- `SetToMax` 仅在 `MaxValue > 0` 或 `m_maxValues` 包含该 tag 时执行

#### AttributeSetDef (属性模板 SO)

```
路径: Assets/Data/Attributes/
Entry 结构:
- Tag: GameplayTag (属性标识)
- BaseValue: float (初始值)
- MaxValue: float (上限, 0=无上限)
```

#### WellKnownAttributeTags (静态常量)

| 常量 | Tag 字符串 | 用途 |
|------|-----------|------|
| `HP` | `"Attribute.HP"` | 生命值 |
| `AP` | `"Attribute.AP"` | 行动点 |
| `Initiative` | `"Attribute.Initiative"` | 先攻值 |
| `MoveSpeed` | `"Attribute.MoveSpeed"` | 移动速度 (米/AP) |

### 2.3 移动层 — Movement 系统

**状态：已完整实现**

#### MovementController

```
依赖: NavMeshAgent + AttributeSet
核心属性:
- Agent: NavMeshAgent 引用
- IsMoving: bool
- RemainingMoveDistance: CurrentAP * MoveSpeed - unpaidDistance

公共方法:
- TryMove(NavMeshPath) → bool : 开始沿路径移动
- TryStartMove(NavMeshPath) → bool : TryMove 别名
- StopMovement() : 停止移动
- PreviewMovementApCost(pathLength) → int : 预估AP消耗
- ResetUnpaidDistance() : 回合切换时重置

事件: MovementStarted / MovementStopped / ApDeducted
```

**AP 扣除机制（累计式）：**
1. 每帧 `Update()` 中计算当前位置与上一帧位置的移动增量
2. 增量累加到 `m_unpaidMoveDistance`
3. 当 `m_unpaidMoveDistance >= MoveSpeed` 时，扣除 1 AP
4. `m_unpaidMoveDistance -= MoveSpeed`，剩余距离带入下次结算
5. 回合切换时 `ResetUnpaidDistance()` 清零未结算距离

#### PathCostCalculator

```
纯静态工具类：
- PathLength(Vector3[] corners) → float : 路径总长度
- ApCost(length, speed) → int : Ceil(length/speed)
- Clip(corners, maxDistance, out reachable, out unreachable) : 路径裁剪
```

#### NavMeshManager

```
单例配置：
- MouseSnapRadius: 鼠标点到 NavMesh 的采样半径 (默认 0.5m)
- OriginSnapRadius: 单位位置到 NavMesh 的采样半径 (默认 2m)
```

#### PathPreview

```
LineRenderer 路径预览：
- 可达段: 绿色虚线 (LineRenderer)
- 不可达段: 红色虚线
- 当前移动路径: 蓝色线
- 距离标签: OnGUI 显示 "X.Xm / Y.Ym"
```

#### CameraController

```
45° 固定视角：
- 跟随: SmoothDamp 缓动
- 聚焦: FocusOn(Transform) → 自动复位手动平移
- 平移: WASD/方向键 + 鼠标中键拖拽
- 缩放: 滚轮 (7m–18m)
- 旋转: 完全锁定 (m_fixedEulerAngles)
- 自动聚焦: 角色开始移动时重置; 手动平移后停止自动聚焦
```

### 2.4 输入层 — Input 系统

**状态：已完整实现**

#### InputController

```
职责: 纯输入接收器，只把原始输入翻译成 SkillInputRequest

流程:
1. 从摄像机做射线检测
2. 根据碰撞对象解析目标 Tag:
   - 读 GameplayTagComponent: Control.Human → k_TargetHuman
   - 读 GameplayTagComponent: Control.AI → k_TargetAI
   - Ground Layer → k_TargetGround
   - 其他 → k_TargetUnknown
3. 将信号 Tag (Hover/PrimaryPressed) + 目标 Tag + 命中信息 打包为 SkillInputRequest
4. 通过 InputReceived 事件分发

> **迁移决议 (2026-05-11):** 原实现通过 `GetComponent<Player1Controller>()` / `GetComponent<EnemyController>()`
> 类型判断。已决议改为读 `GameplayTagComponent` 上的 `Control.Human` / `Control.AI` Tag。
> 这消除了对 Player1Controller/EnemyController 的类型依赖，且天然兼容心控、Charm、托管自动战斗等
> 控制权转移场景。
```

#### SkillInputRequest (readonly struct)

```
字段:
- SignalTag: 输入信号 (Input.Pointer.Hover / Input.Pointer.PrimaryPressed)
- TargetTag: 命中语义 (Input.Target.Ground / Control.Human / Control.AI / Unknown)
- TargetObject: 命中的 GameObject
- WorldPosition: 命中世界坐标
- HasWorldPosition: 是否有有效世界坐标

方法:
- IsSignal(tag) : 检查信号 Tag
- IsTarget(tag) : 检查目标 Tag
```

#### SkillInputTag (静态常量)

| 常量 | Tag 字符串 |
|------|-----------|
| `k_PointerHover` | `Input.Pointer.Hover` |
| `k_PrimaryPressed` | `Input.Pointer.PrimaryPressed` |
| `k_TargetGround` | `Input.Target.Ground` |
| `k_TargetHuman` | `Control.Human` |
| `k_TargetAI` | `Control.AI` |
| `k_TargetUnknown` | `Input.Target.Unknown` |

### 2.5 技能层 — Skill 系统

**状态：核心完成，扩展待实现**

#### 整体流程

```
InputController → SkillInputRequest
    → CombatRoundManager.HandleInputReceived
        → SkillExecutor.HandleInput
            → SkillAbility.HandleInput
                (GroundMoveAbility: 计算路径, 预览, 或执行移动)
```

#### SkillDefinition (ScriptableObject)

```
字段分组:
1. Identity: id, displayName, description
2. Cost & Limits: apCost, cooldown, range
3. Targeting: targetType (Self/SingleEnemy/SingleAlly/GroundPoint/Area)
4. Ability: SkillAbility SO 引用 (可选)
5. Effects: EffectDefinition[] (多个独立Effect资产引用)
6. Tags: skillTags, aiTags (GameplayTag[])
7. Tag Conditions: requiredCasterTags, blockedCasterTags, requiredTargetTags, blockedTargetTags
8. AI: aiBaseWeight
```

**关键实现细节：**
- `Ability` getter：如果 `m_ability != null` 返回配置的 Ability；如果 `targetType == GroundPoint` 返回 `GroundMoveAbility.DefaultInstance`
  - **问题：DefaultInstance 是通过 `CreateInstance<GroundMoveAbility>()` 在运行时创建的 ScriptableObject，不是持久化资产。这违反了 SO 应作为资产存在的原则。**
- `Effects` 返回数组副本，不直接暴露内部引用
- `HasEffectTag(tag)` 遍历所有 Effect 的 Tag 进行精确匹配

#### SkillAbility (抽象基类 ScriptableObject)

```
虚方法:
- GetApCost(context) → int : 默认返回 Skill.ApCost
- CanApply(context) → SkillCastResult : 默认返回 Success
- Apply(context) → SkillCastResult : 默认返回 Success
- HandleInput(context, inputRequest) → SkillCastResult : 默认返回 Success
```

**当前只有一个具体实现：`GroundMoveAbility`**

#### SkillExecutor (MonoBehaviour)

```
职责: 技能执行的统一入口

核心方法:
- CanExecute(SkillExecutionContext) → SkillCastResult
  ├── 检查 Skill 非空
  ├── 检查施法者存活
  ├── 检查 AP 充足 (通过 GetApCost 获取动态 AP 消耗)
  ├── 检查冷却
  ├── 解析目标 (ResolveTarget)
  ├── 检查目标存活 (非 Self / GroundPoint)
  ├── 检查目标能力 (非 GroundPoint)
  ├── 检查阵营匹配
  ├── 检查距离 (非 Self / GroundPoint)
  ├── 检查 Tag 条件 (EvaluateTagConditions)
  └── 检查 Ability.CanApply (若有)

- Execute(SkillExecutionContext) → SkillCastResult
  ├── CanExecute (前置校验)
  ├── Ability.Apply (若有，先执行)
  ├── TrySpend AP (Ability 成功后扣 AP)
  ├── 依次执行 Effects
  └── 记录冷却

- HandleInput(SkillInputRequest) → SkillCastResult
  └── 委托给 ActiveSkill.Ability.HandleInput

- ExecuteAfterMove(skill, target) → SkillCastResult
  └── 移动完成后重新校验目标并执行

- 冷却管理: AdvanceCooldowns() / ResetCooldowns() / GetCooldownRemaining()
- 技能激活: ActivateSkill() / ClearActiveSkill() / FindSkill()
```

**执行顺序（关键）：**
1. `CanExecute` — 全面前置校验
2. `Ability.Apply` — 先执行 Ability（如开始移动）
3. `TrySpend AP` — Ability 成功后扣除 AP（CR-0010 修复后）
4. `Effect.Apply` — 依次执行效果
5. 记录 `cooldown`

#### SkillExecutionContext (struct)

```
工厂方法:
- ForTarget(caster, skill, target) : 目标施放
- ForGroundPoint(caster, skill, position, path) : 地面点施放
- ForInput(caster, skill, inputRequest) : 输入请求执行

属性:
- Caster / CasterAttributes / CasterMovement : 便捷访问
```

#### EffectDefinition (抽象基类 ScriptableObject)

```
字段:
- m_tags: GameplayTag[] (必须至少有一个)
- m_description: 说明

抽象:
- RequiredCapability → ETargetCapability : 目标必须拥有的能力
- Apply(EffectContext) : 执行效果

具体实现:
- DamageEffectDefinition : 扣除 HP，需要 Damageable 能力
- HealEffectDefinition : 恢复 HP，需要 Healable 能力
- AddStatusEffectDefinition : 添加状态（占位），需要 Statusable 能力
- MoveEffectDefinition : 语义标记，需要 Movable 能力
```

**Effect 资产路径：** `Assets/Data/Effects/`

#### EffectContext (struct)

```
字段:
- Caster: GameObject (施法者)
- Target: GameObject (目标)
- CasterExecutor: SkillExecutor
- TargetExecutor: SkillExecutor
- TargetPosition: Vector3? (地面点)
```

#### ETargetCapability (Flags enum)

```
Damageable  = 1 << 0  : 可被伤害
Healable    = 1 << 1  : 可被治疗
Statusable  = 1 << 2  : 可添加状态
Interactable = 1 << 3 : 可交互
Destructible = 1 << 4 : 可破坏
Movable     = 1 << 5  : 可移动
```

#### SkillPlan (struct)

```
设计用途: 描述移动 + 技能复合计划，供玩家预览和 AI 候选评估

字段:
- MovementSkill / PrimarySkill (SkillDefinition)
- PrimaryTarget (GameObject)
- MoveDestination (Vector3)
- MovePath (NavMeshPath)
- MoveApCost / PrimaryApCost / TotalApCost (int)
- CanExecutePrimaryThisTurn / IsAlreadyInRange / IsMovementOnly (bool)
```

**当前状态：** `SkillPlan` 结构已定义但**尚未被任何代码使用**。它是 Docs/08 §7 AI 候选系统中描述的核心数据结构，但 `EnemyTurnRunner` 当前仍使用硬编码逻辑。

### 2.6 战斗层 — Combat 系统

**状态：核心完成**

#### CombatRoundManager

```
职责: 回合管理、状态机、玩家可控块、输入分发

状态机流程:
StartCombat → BuildTurnOrder (按 Initiative 降序, 连续玩家=可控块)
    → StartNextRound (回满AP, 推进冷却, 清空结束标记)
        → AdvanceTurn
            ├── 队首玩家 → RefreshControllableBlock → TrySelectPlayer
            │   └── 玩家结束(空格) → TryEndUnitRound → AdvanceTurn
            └── 队首敌人 → EnemyTurnCoroutine
                    └── enemyTurnRunner.RunTurn → TryEndUnitRound → AdvanceTurn

可控块规则 (Q-0024):
- 连续的玩家单位视为一个"可控块"
- 块内自由切换 (数字键快捷选择 / 点击角色；快捷键数量不代表队伍上限)
- 队首为敌人时，当前块必须全部结束

调试开关: enemyFirstForDebug (默认 false)
```

**关键实现细节：**
- `CacheUnits()` 通过 `FindObjectsOfType` 收集所有 Player1Controller 和 EnemyController
- `BuildTurnOrder()` 按 Initiative 降序排序，使用 `ThenBy(PartySlot)` 处理平局
- `ResolveBasicAttackSkill()` / `ResolveBasicMoveSkill()` 使用 `AssetDatabase.LoadAssetAtPath`（仅 Editor 有效）
- `ValidateUnitSkillComponents()` 在 StartCombat 时检查每个单位的 SkillExecutor 和 basic_move 技能
- `TrySelectPlayer()` 选中角色时自动激活其 basic_move 技能
- `HandleInputReceived()` 将输入分发给选中角色的 SkillExecutor

#### CombatMovementResolver

```
职责: 纯静态工具类，统一处理 NavMesh 移动、技能范围进入、AP 预估、追击退化

核心方法:
- TryGetNavMeshPosition(position, radius, out pos) → bool
- TryBuildCompletePath(origin, dest, out path) → bool
- IsInRange(caster, target, range) → bool
- IsDestinationOpen(dest, radius, players, enemies, excludeEnemy) → bool
- TryFindAttackDestination(corners, target, range, out dest) → bool
- TryFindFarthestReachablePoint(corners, maxDist, out dest) → bool
- EstimateMoveApCost(length, speed, currentAp, unpaidDist) → int
- Resolve(...) → SkillPositioningResult (主要入口)

SkillPositioningResult:
- IsAlreadyInRange / CanReachRange / HasFallback
- Destination / MovePath / MoveApCost / TotalApCost / MoveLength
- FallbackDestination / FallbackPath / FallbackMoveApCost
- Failure (EPositioningFailure enum)
```

#### EnemyTurnRunner

```
当前 MVP 实现:
- RunTurn(enemy, players, enemies, skill)
  1. 找最近存活玩家
  2. 如果不在技能范围内 → TryMoveTowardTarget (移动入范围或靠近)
  3. 等待移动结束
  4. ExecuteAfterMove (通过 SkillExecutor 执行技能)

软占位:
- 计算攻击目标点时，绕目标环形采样避开其他单位位置
- 追击 fallback 点时同样采样避开

问题:
- 只支持单个技能 (参数传入)，不支持技能候选评估
- 不使用 AIProfile 做决策
- 所有敌方都使用相同行为: "追最近的 → 攻击"
- bridge helpers 同时引用 legacy EnemyController API 和新 AttributeSet/MovementController
```

### 2.7 AI 层

#### AIProfile (ScriptableObject)

```
字段:
- Role: EAIRole (Aggressive/Support/Healer) — 默认倾向
- SkillTagWeights: TagWeightEntry[] — 技能 Tag 权重
- TargetTagWeights: TagWeightEntry[] — 目标 Tag 权重
- StatusTagWeights: TagWeightEntry[] — 状态 Tag 权重
- HealHpThreshold: float (0–1) — 治疗阈值
- HealUrgencyBonus: float — 低于阈值时治疗加权

TagWeightEntry:
- Tag: GameplayTag
- Weight: float (乘数, 默认1f)
```

**当前状态：** AIProfile 数据结构完整但**尚未被 EnemyTurnRunner 使用**。敌方 AI 仍使用 MVP 硬编码逻辑。

#### EAIRole

```
Aggressive: 进攻倾向 — 提高伤害、击杀、靠近目标权重
Support: 支援倾向 — 提高 buff、保护、集火优势权重
Healer: 治疗倾向 — 友方低血量时治疗优先；无治疗目标退化为支援/攻击
```

### 2.8 角色层

#### Player1Controller

```
职责: 玩家角色视觉状态、属性便捷访问、旧 API 兼容桥接

组件栈:
GameObject
├── AttributeSet (MB)
├── MovementController (MB) [RequireComponent NavMeshAgent, AttributeSet]
├── SkillExecutor (MB)
└── Player1Controller (MB) [RequireComponent NavMeshAgent]

便捷属性(桥接 AttributeSet): CurrentAP, MaxAP, CurrentHP, MaxHP, Initiative, MoveSpeedMetersPerAp
便捷属性(桥接 MovementController): IsMoving, RemainingMoveDistance
便捷方法: BeginRound(), TryEndRound(), TrySpendAP(), TryMove(), StopMovement(), TakeDamage(), Heal()
```

#### EnemyController

```
职责: 敌方角色视觉、AI 档案引用、HP=0 时自动销毁、旧 API 兼容桥接

组件栈: 同 Player1Controller

额外:
- AIProfile 引用
- AttributeDepleted 事件监听 → HP 归零时 Destroy(gameObject)
- FlashTurn() 协程: 0.5秒变色提示
```

#### EnemySpawner

```
临时敌人生成器 (Awake时自毁):
- 创建 Capsule + AttributeSet + MovementController + SkillExecutor + EnemyController
- 使用 Testing_AddAttribute 设置属性
- OverrideFactionForTesting(EFaction.Enemy)
- SetSkills(m_defaultSkills) 配置技能列表

问题: 临时占位方案，应被关卡数据替换
```

---

## 3. 组件依赖图

```
GameObject (玩家/敌人/环境目标)
├── AttributeSet (MB)              ← 数据层
│   └── AttributeSetDef (SO)       ← 属性模板(可选)
├── MovementController (MB)        ← 移动层
│   └── NavMeshAgent (Unity)       ← 寻路
├── SkillExecutor (MB)             ← 技能执行层
│   └── SkillDefinition (SO)       ← 技能配置
│       ├── SkillAbility (SO)      ← 技能行为
│       │   └── GroundMoveAbility  ← 地面移动
│       └── EffectDefinition (SO)  ← 效果列表
│           ├── DamageEffectDefinition
│           ├── HealEffectDefinition
│           ├── AddStatusEffectDefinition
│           └── MoveEffectDefinition
├── GameplayTagComponent (MB)      ← Tag 层
│   └── GameplayTagSet             ← 运行时 Tag
└── Player1Controller / EnemyController (MB) ← 角色外观层
```

---

## 4. 数据流总结

### 玩家点击地面移动
```
Mouse Click → InputController (射线检测)
  → SkillInputRequest(signal=PrimaryPressed, target=Ground, pos=世界坐标)
    → CombatRoundManager.HandleInputReceived
      → SelectedPlayer.SkillExecutor.HandleInput
        → ActiveSkill.Ability.HandleInput (GroundMoveAbility)
          → NavMesh.CalculatePath → PathCostCalculator
          → SkillExecutor.Execute(context with path)
            → CanExecute (校验 AP, 冷却, Tag)
            → Ability.Apply (TryStartMove via MovementController)
            → TrySpend AP
            → (无 Effect, basic_move 无效果)
```

### 敌方 AI 行动
```
CombatRoundManager.AdvanceTurn → EnemyTurnCoroutine
  → EnemyTurnRunner.RunTurn
    → FindNearestLivingPlayer
    → CombatMovementResolver.Resolve (计算定位)
    → TryMoveTowardTarget (移动 + 软占位)
    → 等待移动结束
    → SkillExecutor.ExecuteAfterMove (执行技能)
```

### 技能执行核心流程
```
SkillExecutor.Execute(context)
├── CanExecute(context)  ← 通用校验
│   ├── Skill 非空
│   ├── Caster 存活 (AttributeSet.IsAlive)
│   ├── AP 充足 (含 Ability.GetApCost)
│   ├── 冷却检查
│   ├── 目标解析
│   ├── 目标存活 (非 Self/GroundPoint)
│   ├── 目标能力 (非 GroundPoint, 检查 Effect.RequiredCapability)
│   ├── 阵营匹配
│   ├── 距离检查 (非 Self/GroundPoint)
│   ├── Tag 条件 (caster/target required/blocked)
│   └── Ability.CanApply
├── Ability.Apply (若存在)     ← 先执行 Ability
├── TrySpend AP                ← Ability 成功后扣 AP
├── Effect.Apply × N           ← 依次执行效果
└── 记录冷却
```

---

## 5. 测试覆盖

### 已有测试
| 测试文件 | 覆盖内容 | 测试数 |
|---------|---------|--------|
| `GameplayTagTests.cs` | Tag 校验、等值、匹配 (Exact/Prefix)、TagSet 增删查、来源追踪、Prefix 匹配跨段边界、TagQuery 条件 | 52 |
| `SkillExecutorTests.cs` | CanExecute/Execute 各种失败路径、AP 消耗、冷却、Effect 执行 | 若干 |
| `SkillDefinitionTests.cs` | SkillDefinition 字段、Effect 引用 | 若干 |
| `CombatMovementResolverTests.cs` | NavMesh 定位、范围判断、路径计算 | 若干 |

### 测试缺口
- AIProfile 权重计算（无测试）
- CombatRoundManager 可控块逻辑（无测试）
- MovementController AP 累计扣除（无测试）
- GroundMoveAbility 输入处理（无测试）
- InputController 射线检测目标解析（无测试）
- Status 系统（未实现，无法测试）
- AI 候选评分（未实现，无法测试）
- Config Validation 校验工具（无测试）

---

## 6. 代码规范符合度

### 已符合
- ✅ 所有字段使用 `m_camelCase` 命名
- ✅ 枚举使用 `E` 前缀 (EFaction, ESkillTargetType, ETagMatchMode 等)
- ✅ 常量使用 `k_PascalCase` (SkillInputTag, WellKnownAttributeTags)
- ✅ `[SerializeField] private` 替代 public 字段
- ✅ 命名空间: MiniChess.Combat, MiniChess.Combat.Skills, MiniChess.GameplayTags
- ✅ ScriptableObject 不存储运行时状态

### 需改进
- ⚠️ `EnemyTurnRunner` 中 bridge helpers 同时支持新旧 API — 迁移完成后应移除 legacy 分支
- ⚠️ `CombatRoundManager` 使用 `#if UNITY_EDITOR` + `AssetDatabase` — 运行时加载应使用 Resources 或 Addressables
- ⚠️ 部分 TODO 注释引用已废弃类名

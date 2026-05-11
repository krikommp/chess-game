# 16 - 重构执行计划

> 2026-05-11。基于代码 vs 文档差距审计，按依赖顺序排列的重构步骤。每个步骤包含：现状、目标、涉及文件、验证方式。后续 Agent 按顺序逐项推进。

## 执行规则

- **每步独立提交**：完成一步 → 验证 → 提交，不跨步合并
- **Step 1-2 可并行**（无相互依赖），其余严格顺序
- **每个 Step 执行前**：先读涉及文件的最新状态，本计划是快照，代码可能已变
- **Step 11 是终点目标**：删除 Player1Controller + EnemyController，所有单位统一组件栈

---

## Step 1: GroundMoveAbility.DefaultInstance 移除

**CR/IS:** CR-0012 / IS-0004  
**严重级别:** P0  
**依赖:** 无  
**解锁:** Step 3（系统技能需要显式资产引用）

### 现状

`SkillDefinition.cs:60-65`:
```csharp
return m_targetType == ESkillTargetType.GroundPoint
    ? GroundMoveAbility.DefaultInstance : null;
```

`GroundMoveAbility.cs:10-24`:
```csharp
public static GroundMoveAbility DefaultInstance => s_defaultInstance ??= CreateInstance<GroundMoveAbility>();
```
运行时 `CreateInstance<ScriptableObject>()` 破坏 SO 资产约定。

### 目标

1. 确保 `Assets/Data/Skills/GroundMoveAbility.asset` 存在且有效 → 如果不存在，通过 Unity Editor 创建
2. 确保 `basic_move.asset` 的 `m_ability` 字段已拖入上述资产（通过 Unity Editor，不是代码）
3. 修改 `SkillDefinition.cs:60-65`：GroundPoint 类型但 `m_ability == null` 直接返回 null，不 fallback
4. 删除 `GroundMoveAbility.cs` 的 `DefaultInstance` 属性 + `s_defaultInstance` 字段
5. 在 Config Validation 中增加检查：`targetType == GroundPoint` 的技能必须配置 Ability

### 涉及文件

- `Assets/Scripts/Combat/Skills/SkillDefinition.cs` (60-65行)
- `Assets/Scripts/Combat/Skills/GroundMoveAbility.cs` (10-24行)
- `Assets/Data/Skills/basic_move.asset` (Inspector 操作)
- (可选) `Assets/Data/Skills/GroundMoveAbility.asset` (确认存在)

### 验证

- `GroundMoveAbility.DefaultInstance` 编译错误（属性已删除）
- `basic_move.asset` 的 Ability 字段指向具体资产
- 玩家点击地面仍能正常移动（`basic_move` 工作正常）

---

## Step 2: SkillExecutor.CollectTags 移除重复 Faction 同步

**CR/IS:** CR-0022 / IS-0007  
**严重级别:** P1  
**依赖:** 无  
**解锁:** 无（独立修复）

### 现状

`SkillExecutor.cs:401-407`:
```csharp
var attr = obj.GetComponent<AttributeSet>();
if (attr != null)
{
    var factionTag = new GameplayTag(attr.Faction == EFaction.Player
        ? "Faction.Player" : "Faction.Enemy");
    outTags.Add(factionTag, "SkillExecutor.FactionAutoSync");
}
```

但 `AttributeSet.Awake()` 中 `SyncFactionTag()` 已经做了同样的事。两处独立添加，重复逻辑。

### 目标

删除 `CollectTags` 中的手动 Faction 同步代码块（上述 6 行）。`AttributeSet.SyncFactionTag()` 作为唯一 Faction Tag 同步点。

### 涉及文件

- `Assets/Scripts/Combat/Skills/SkillExecutor.cs` (390-408行)

### 验证

- 编译通过
- 单位仍然有 `Faction.Player` / `Faction.Enemy` Tag（由 AttributeSet 维护）
- `rg "SkillExecutor.FactionAutoSync" Assets/` 不再出现

---

## Step 3: 创建系统技能资产 + Effect 函数配置 + RoundPhaseManager

**CR/IS:** CR-0024  
**严重级别:** P0  
**依赖:** Step 1（Ability 显式引用）  
**解锁:** Step 4/5/6

### 现状

- `sys_round_start`、`sys_turn_end`、`sys_on_death` 技能资产不存在
- 以下 EffectFunction 尚未实现：`RestoreAttribute`、`ResetMovement`、`AdvanceCooldowns`、`TriggerStatusTick`、`DecrementStatusDuration`、`DeregisterFromCombat`、`DeathVisual`、`DestroyGameObject`
- `RoundPhaseManager` MonoBehaviour 不存在

### 目标

#### 3a. 创建 EffectFunction

> **设计决议 (2026-05-12):** `EffectDefinition` 是统一 data-only ScriptableObject，不创建 `RestoreAttributeEffect : EffectDefinition` 等用户派生类。Effect 通过配置一个静态 `EffectFunction` 完成判断、计算和实际修改；该函数类同时提供 `Compute` 与 `Apply`，参数通过 Effect 数据传入。Ability 按阶段直接调用 Effect，不引入 `EffectOperation` / `HandlerId` / `EffectRunner` 分发层。详见 `OPEN_QUESTIONS.md` §Ability-Effect 设计决议。

> **Ability 决议 (2026-05-12):** `SkillAbility` 是开放给用户编写的流程类。基类只提供 Tag / Cost / Cooldown / Effect 的通用检查与应用 helper，当前阶段由具体 Ability 显式调用；如果后续发现流程高度重复，再提取自动流程或模板方法。

**EffectFunction: RestoreAttribute** (Instant):
- `[SerializeField] private GameplayTag m_attributeTag;` — 要恢复的属性（如 `Attribute.AP`）
- `[SerializeField] private ERestoreMode m_mode;` — `ToMax` / `ByValue`
- `[SerializeField] private float m_value;` — ByValue 模式下的恢复量
- `RequiredCapability` → `Statusable`（所有单位都应能接受系统维护）
- `Apply()` → 根据 mode 调用 `attr.SetToMax(tag)` 或 `attr.Modify(tag, +value)`

**EffectFunction: ResetMovement** (Instant):
- `RequiredCapability` → `Movable`
- `Compute()` → 总是返回成功
- `Apply()` → `movement?.ResetUnpaidDistance()`

**EffectFunction: AdvanceCooldowns** (Instant):
- `RequiredCapability` → `Statusable`
- `Compute()` → 总是返回成功
- `Apply()` → `executor?.AdvanceCooldowns()`

**EffectFunction: TriggerStatusTick** (Instant):
- `[SerializeField] private EStatusTickPhase m_phase;` — `TurnStart` / `TurnEnd`
- `RequiredCapability` → `Statusable`
- `Compute()` → 总是返回成功
- `Apply()` → 遍历 `executor.ActiveEffects`，对匹配 phase 的 tick

**EffectFunction: DecrementStatusDuration** (Instant):
- `RequiredCapability` → `Statusable`
- `Compute()` → 总是返回成功
- `Apply()` → 遍历 `executor.ActiveEffects`，`remainingRounds -= 1`，归零的移除

**EffectFunction: DeregisterFromCombat** (Instant):
- `RequiredCapability` → `Statusable`
- `Compute()` → 总是返回成功
- `Apply()` → `CombatRoundManager` 实例从 turnOrder 移除 `context.Target`

**EffectFunction: DeathVisual** (Instant):
- `RequiredCapability` → `Statusable`
- `Compute()` → 总是返回成功
- `Apply()` → Phase 1 用 `Debug.Log` + 可选 Material 颜色变化；后续接正式动画

**EffectFunction: DestroyGameObject**:
- `[SerializeField] private float m_delaySeconds = 0.5f;`
- `RequiredCapability` → `Statusable`
- `Apply()` → `Destroy(context.Target, m_delaySeconds)`

#### 3b. 创建 RoundPhaseManager (`Assets/Scripts/Combat/RoundPhaseManager.cs`)

```csharp
public class RoundPhaseManager : MonoBehaviour
{
    [SerializeField] private CombatRoundManager m_roundManager;
    [SerializeField] private SkillDefinition m_sysRoundStart;
    [SerializeField] private SkillDefinition m_sysTurnEnd;
    [SerializeField] private SkillDefinition m_sysOnDeath;

    void OnEnable()
    {
        m_roundManager.RoundStarted += OnRoundStarted;
        m_roundManager.UnitTurnEnded += OnUnitTurnEnded;
        // UnitTurnStarted 中检测死亡？不在这里——
        // AttributeSet.AttributeDepleted(HP) → 触发 sys_on_death
    }

    void OnRoundStarted(int round)
    {
        foreach (var unit in m_roundManager.TurnOrder)
        {
            var executor = unit.GetComponent<SkillExecutor>();
            executor?.Execute(SkillExecutionContext.ForTarget(executor, m_sysRoundStart, unit));
        }
    }

    void OnUnitTurnEnded(GameObject unit)
    {
        var executor = unit.GetComponent<SkillExecutor>();
        executor?.Execute(SkillExecutionContext.ForTarget(executor, m_sysTurnEnd, unit));
    }

    public void ExecuteOnDeath(GameObject unit)
    {
        var executor = unit.GetComponent<SkillExecutor>();
        executor?.Execute(SkillExecutionContext.ForTarget(executor, m_sysOnDeath, unit));
    }
}
```

#### 3c. 创建技能资产（通过 Unity Editor）

- `Assets/Data/Skills/sys_round_start.asset`:
  - id: `sys_round_start`, targetType: Self, Costs: [], Cooldowns: []
  - effects: [Effect(Function=RestoreAttribute, AP, ToMax), Effect(Function=ResetMovement), Effect(Function=AdvanceCooldowns), Effect(Function=TriggerStatusTick, TurnStart)]
- `Assets/Data/Skills/sys_turn_end.asset`:
  - id: `sys_turn_end`, targetType: Self, Costs: [], Cooldowns: []
  - effects: [Effect(Function=TriggerStatusTick, TurnEnd), Effect(Function=DecrementStatusDuration)]
- `Assets/Data/Skills/sys_on_death.asset`:
  - id: `sys_on_death`, targetType: Self, Costs: [], Cooldowns: []
  - effects: [Effect(Function=DeregisterFromCombat), Effect(Function=DeathVisual), Effect(Function=DestroyGameObject)]
  - 注意：targetType=Self 意味着 caster=target=死亡单位自身

### 涉及文件

- 新建/扩展 EffectFunction 静态函数类
- 新建 `RoundPhaseManager.cs`
- 新建 3 个技能 .asset（Unity Editor 操作）
- 新建配套 Effect .asset（Unity Editor 操作）

### 验证

- 编译通过，统一 EffectDefinition 资产可选择上述 EffectFunction
- `sys_round_start` / `sys_turn_end` / `sys_on_death` 资产可在 Project 窗口看到
- RoundPhaseManager Inspector 可拖入技能引用

---

## Step 4: 恢复回合开始 AP 与移动预算

**CR/IS:** CR-0026 / IS-0020  
**严重级别:** P0  
**依赖:** Step 3（sys_round_start 存在）  
**解锁:** 第二回合能正常玩

### 现状

`CombatRoundManager.StartNextRound()` 第 107-110 行：
```csharp
foreach (var go in m_turnOrder)
{
    if (go == null || !IsAlive(go)) continue;
    go.GetComponent<SkillExecutor>()?.OnRoundStart();
}
```
`SkillExecutor.OnRoundStart()` 只推进冷却和 tick 持续效果，没有恢复 AP，没有重置移动预算。

### 目标

1. 在场景 `[CombatRoundManager]` 对象上（或 Systems 对象上）挂载 `RoundPhaseManager`
2. 拖入 `m_roundManager`、`m_sysRoundStart`、`m_sysTurnEnd`、`m_sysOnDeath` 引用
3. 移除 `StartNextRound()` 中的 `SkillExecutor.OnRoundStart()` 直接调用循环
4. 确认 `Effect(Function=RestoreAttribute, AP, ToMax)` → `sys_round_start` 链路完整

### 涉及文件

- `Assets/Scripts/Combat/CombatRoundManager.cs` (107-110行)
- `Assets/Scripts/Combat/RoundPhaseManager.cs` (Step 3 创建)
- 场景配置

### 验证

- 第一轮消耗 AP 后，第二轮开始时 AP 恢复到 MaxAP
- `MovementController.ResetUnpaidDistance()` 被调用（移动预算不跨轮残留）
- `State.APBlocked` Tag 可阻断 AP 恢复（后续验证）

---

## Step 5: 死亡技能化

**CR/IS:** IS-0017  
**严重级别:** P0  
**依赖:** Step 3（sys_on_death 存在）  
**解锁:** Step 11（删除 EnemyController 的 OnAttributeDepleted）

### 现状

- `EnemyController.OnAttributeDepleted` → 直接 `Destroy(gameObject)`
- `Player1Controller.OnAttributeDepleted` → 空方法体

### 目标

1. 修改 `AttributeSet` 的 `AttributeDepleted` 触发逻辑：HP 归零时额外调用 `RoundPhaseManager.Instance.ExecuteOnDeath(gameObject)`
   - 或者由某个订阅 `AttributeDepleted` 的 handler 调用
2. 修改 `EnemyController.OnAttributeDepleted`：删除 `Destroy(gameObject)` 调用，保留空方法（等 Step 11 彻底删除）
3. `Player1Controller.OnAttributeDepleted`：保持为空（等 Step 11 彻底删除）
4. 死亡流程变为：`AttributeSet.HP <= 0` → `AttributeDepleted.HP` → `RoundPhaseManager.ExecuteOnDeath(unit)` → `sys_on_death` → [注销, 死亡VFX, Destroy]

### 涉及文件

- `Assets/Scripts/Combat/AttributeSet.cs`
- `Assets/Scripts/Combat/EnemyController.cs` (删除 OnAttributeDepleted 内的 Destroy)
- `Assets/Scripts/Combat/RoundPhaseManager.cs` (需支持 ExecuteOnDeath)

### 验证

- 敌方 HP 归零时触发 sys_on_death 流程（Console 有死亡日志）
- 敌方死亡后不再出现在 turnOrder
- 延迟 Destroy 正常执行

---

## Step 6: CombatRoundManager 精简到纯事件广播器

**CR/IS:** CR-0015 / IS-0003  
**严重级别:** P0  
**依赖:** Step 3/4/5  
**解锁:** 架构完整性

### 目标

1. `CombatRoundManager` 精简到 ~120 行：
   - 移除 `StartNextRound()` 中对 `SkillExecutor.OnRoundStart()` 的直接调用
   - `CheckVictory()` 逻辑保留但可标记为后续拆到 `VictoryConditionEvaluator`
2. `CollectUnits()` 保持不变（通过 `CombatUnit` 标记）
3. `BuildTurnOrder()` / `RefreshControllableBlock()` / `AdvanceToNextUnit()` 保持不变
4. 所有游戏逻辑通过订阅事件的 handler 执行

### 涉及文件

- `Assets/Scripts/Combat/CombatRoundManager.cs`

### 验证

- `CombatRoundManager` 只有 ~120 行
- 不直接包含任何 AP/技能/冷却/移动/死亡 逻辑
- 仅有的"逻辑"是收集单位、按先攻排序、维护可控块、检测胜负

---

## Step 7: 场景迁移

**CR/IS:** CR-0025 / IS-0019  
**严重级别:** P0  
**依赖:** Step 3-6（系统对象上有正确的组件）  
**解锁:** Play 模式可验证

### 目标

1. **玩家单位**：在场景中每个玩家对象上 Add Component `CombatUnit` + `GameplayTagComponent`。给 `GameplayTagComponent` 添加初始 Tag：`Control.Human`、`Faction.Player`
2. **敌方单位**：`EnemySpawner.SpawnEnemy()` 中 `MovementController` 之前添加：
   ```csharp
   go.AddComponent<CombatUnit>();
   var tags = go.AddComponent<GameplayTagComponent>();
   tags.AddTag(new GameplayTag("Control.AI"), this);
   // Faction.Enemy 由 AttributeSet.OverrideFactionForTesting 的 SyncFactionTag 自动添加
   ```
3. **系统对象**：在 `[CombatRoundManager]` 上 Add Component `RoundPhaseManager` + `UnitTurnHandler` + `EnemyTurnRunner`。拖入所有 Inspector 引用
4. **清理**：`CombatRoundManager` 上的旧序列化字段（Unity 自动忽略但需清理）
5. **GAP-M (IS-0022)**：确认 `UnitTurnHandler.cs.meta` 包含 `MonoImporter` 块后 `git add`

### 涉及文件

- `Assets/Scenes/SampleScene.unity`
- `Assets/Scripts/Combat/EnemySpawner.cs`
- `Assets/Scripts/Combat/UnitTurnHandler.cs.meta`

### 验证

- Play 模式：`StartCombat()` 收集到 >0 个单位
- 回合顺序正常，玩家可操作
- 敌方 AI 可执行回合
- `git status` 显示 `.meta` 已纳入跟踪

---

## Step 8: UnitTurnHandler 选择校验

**CR/IS:** CR-0027 / IS-0021  
**严重级别:** P1  
**依赖:** Step 7（场景有 UnitTurnHandler）  
**解锁:** 先攻规则正确

### 现状

`UnitTurnHandler.cs`:
- `SelectUnit()` 不检查 `ControllableUnits.Contains(unit)` 和 `HasEndedRound(unit)`
- `OnInputReceived()` 点击玩家时直接调 `SelectUnit()`，绕过数字键走的 `ControllableUnits` 检查

### 目标

1. 增加 `TrySelectUnit(GameObject unit)` 方法：统一检查 `ControllableUnits.Contains(unit)` + `!HasEndedRound(unit)`
2. `OnUnitTurnStarted()`、数字键切换、点击选择都走同一个 `TrySelectUnit()` 入口
3. 非法点击忽略或输出 debug 日志，不把选择状态切出当前可控块

### 涉及文件

- `Assets/Scripts/Combat/UnitTurnHandler.cs`

### 验证

- `P1 → Enemy → P2` 顺序中，玩家点击 P2 时不会被选中
- Console 输出"不在当前可控块"的 debug 信息
- 数字键和点击行为一致

---

## Step 9: EnemySpawner 改用正式 API

**CR/IS:** CR-0016 / IS-0008  
**严重级别:** P1  
**依赖:** Step 7（Spawner 已加 CombatUnit）  
**解锁:** Build 后敌人正常

### 目标

1. 创建 `EnemyMelee_Def` AttributeSetDef 资产（HP=100, AP=6, Initiative=5, MoveSpeed=2, Faction=Enemy）
2. `EnemySpawner.SpawnEnemy()` 改为 `attr.BuildFromDefinition(m_enemyDef)` 替代 `Testing_AddAttribute` 逐个调用
3. `OverrideFactionForTesting` 由 AttributeSetDef 的 Faction 配置替代

### 涉及文件

- `Assets/Scripts/Combat/EnemySpawner.cs`
- 新建 `Assets/Data/Units/EnemyMelee_Def.asset`（或类似路径）

### 验证

- 非 Editor 环境（或移除 `DEVELOPMENT_BUILD` 符号后）EnemySpawner 仍正常工作
- `rg "Testing_AddAttribute|OverrideFactionForTesting" Assets/Scripts/Combat/EnemySpawner.cs` 返回空

---

## Step 10: AI 候选框架

**CR/IS:** CR-0014 / IS-0002  
**严重级别:** P0  
**依赖:** Step 4（AP 恢复正常）, Step 7（场景可跑）, Step 3（技能资产存在）  
**解锁:** Step 11（EnemyTurnRunner 完全走 SkillExecutor）

### 概要

详细设计见 `Docs/13_FUTURE_DESIGN.md` §2。实现要点：

1. **AIActionCandidate** (struct)：Skill + Target + MoveDestination + MoveApCost + SkillApCost + Score + FailureReason
2. **AIScoreBreakdown** (struct)：BaseWeight + SkillTagScore + TargetTagScore + HpUrgencyScore + DistanceScore + ApEfficiencyScore
3. **AIActionEvaluator** (static class)：`Evaluate(GameObject unit, IReadOnlyList<GameObject> allTargets)` → 最优候选
   - 候选生成：遍历 `executor.AvailableSkills` × 合法目标
   - 可用性过滤：AP/冷却/目标存活/范围/Tag 条件
   - 评分：基础权重 + AIProfile Tag 权重 + HP 紧迫度 + 距离/AP 效率
   - 选择：最高分，不可用时 fallback 靠近
4. **EnemyTurnRunner 重构**：不再硬编码 `FindNearestEnemy + MoveTowardTarget`，改为：
   - 收集所有挂载 `SkillExecutor` 的目标
   - 调用 `AIActionEvaluator.Evaluate()`
   - 通过 `SkillExecutor.Execute(ctx)` 执行最优候选
5. **AIProfile 接入**：`AIUnitConfig` 组件（或 EnemyTurnRunner 的按单位配置表）提供 AIProfile 引用
6. **Debug 输出**：可开关，输出候选列表、评分明细、最终选择原因

### 涉及文件

- 新建 `Assets/Scripts/Combat/AI/AIActionCandidate.cs`
- 新建 `Assets/Scripts/Combat/AI/AIActionEvaluator.cs`
- 修改 `Assets/Scripts/Combat/EnemyTurnRunner.cs`
- (可选) 新建 `Assets/Scripts/Combat/AI/AIUnitConfig.cs`

### 验证

- 只有 `basic_attack` 时，行为与当前 MVP 基本一致
- 加入治疗/buff 技能后，敌方能选择非攻击行动
- AIProfile 权重变化后，AI 行为随之变化
- AI 日志输出结构化，可复盘决策过程

---

## Step 11: 删除 Player1Controller + EnemyController

**CR/IS:** CR-0027 隐含, IS-0005  
**严重级别:** P0  
**依赖:** Step 5（死亡技能化）+ Step 7 + Step 10（AI 走 SkillExecutor）+ Step 8（选择校验）  
**解锁:** 架构清理终点

### 前置检查

执行前确认：
- [ ] Step 5 完成：死亡不再由 EnemyController 处理
- [ ] Step 10 完成：EnemyTurnRunner 完全通过 SkillExecutor 执行
- [ ] Step 7 完成：场景所有单位有 CombatUnit + GameplayTagComponent
- [ ] Step 8 完成：UnitTurnHandler 通过 ControllableUnits 校验，不依赖 Player1Controller 类型
- [ ] `InputController.ResolveTargetTag` 改为读 `GameplayTagComponent` 的 `Control.Human`/`Control.AI`

### 目标

1. **提取视觉组件**：创建 `UnitVisualController`（MonoBehaviour）
   - 职责：Default/Hovered/Selected 颜色切换、FlashTurn 闪红
   - 从 Player1Controller/EnemyController 迁移视觉代码
2. **创建 AIUnitConfig**（MonoBehaviour）：
   - `[SerializeField] private AIProfile m_aiProfile;` — 挂到 AI 单位上
3. **修改 InputController.ResolveTargetTag**：
   - `GetComponent<Player1Controller>()` → `GetComponent<GameplayTagComponent>()?.HasTag(Control.Human/Prefix)`
   - `GetComponent<EnemyController>()` → `GetComponent<GameplayTagComponent>()?.HasTag(Control.AI/Prefix)`
4. **删除** `Player1Controller.cs` 和 `EnemyController.cs`
5. **清理** 所有 `GetComponent<Player1Controller>()` / `GetComponent<EnemyController>()` 引用
6. 确认 `EFaction` 枚举不被 Controller 独有（如被多处引用，迁移到 `AttributeSet.cs` 或 `CombatRoundManager.cs`）

### 涉及文件

- 删除：`Player1Controller.cs`, `EnemyController.cs`
- 新建：`UnitVisualController.cs`, `AIUnitConfig.cs`
- 修改：`InputController.cs`, `UnitTurnHandler.cs`, `EnemyTurnRunner.cs`, 所有引用 Controller 类型的文件

### 验证

- `rg "Player1Controller|EnemyController" Assets/Scripts/` 返回空
- 所有现有测试通过
- Play 模式：玩家可操控、敌方 AI 正常、死亡流程正常
- 最终单位组件栈：
  ```
  任何参战单位:
    CombatUnit
    GameplayTagComponent  (Control.Human/AI + Faction.Player/Enemy)
    AttributeSet
    MovementController (+ NavMeshAgent)
    SkillExecutor
    UnitVisualController   (可选)
    AIUnitConfig           (仅 AI 单位)
  ```

---

## 剩余步骤 (P2/P3)

- **Step 12:** TagRegistry 改用 Dictionary 索引 (CR-0021 / IS-0012)
- **Step 13:** 完整配置校验工具 (CR-0019 / IS-0010)
- **Step 14:** 战斗事件总线 (CR-0020 / IS-0016)

---

## 依赖链总览

```
Step 1 (DefaultInstance)  ──┐
                            ├──→ Step 3 (系统技能) ──┬──→ Step 4 (AP恢复)
Step 2 (Faction同步)  ──┘                            ├──→ Step 5 (死亡技能化)
                                                     └──→ Step 6 (CRManager精简)
                                                              │
                         Step 7 (场景迁移) ←──────────────────┘
                              │
                    ┌─────────┼─────────┐
                    ▼         ▼         ▼
              Step 8     Step 9     Step 10 (AI候选框架)
           (选择校验)  (Spawner)        │
                    │         │         │
                    └─────────┴────┬────┘
                                   ▼
                          Step 11 (删除Controller)
```

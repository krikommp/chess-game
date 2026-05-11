# 12 - 代码问题清单 (Issues)

> 2026-05-10 代码审查发现的问题，按严重级别分组。每个问题包含现状、影响和建议修复方向。**本文档中的所有问题留待用户后续处理。**

---

## P0 — 阻塞性架构问题

### IS-0001 Status 系统完全缺失

**严重级别：** P0（阻塞所有 buff/debuff/heal-over-time 功能）

**现状：**
- `AddStatusEffectDefinition` 只有占位 `Apply()` 方法，打印一行日志后无任何实际行为
- 不存在 `StatusDefinition` ScriptableObject
- 不存在 `StatusComponent` MonoBehaviour（用于挂载运行时的持续状态）
- 不存在 Status 生命周期管理（添加、过期移除、Tag 自动清理）
- `HealEffectDefinition.Apply()` 虽然已实现，但没有 Status 系统就无法实现持续恢复（HoT）

**影响：**
- `guarding_shout`（buff 技能）完全无法实现
- `crippling_hex`（debuff 技能）完全无法实现
- 任何持续多轮的技能效果都无法实现
- AI 中的 `Support` / `Healer` 角色缺少核心验证手段

**涉及文件：**
- `Assets/Scripts/Combat/Skills/AddStatusEffectDefinition.cs`
- 待创建：`StatusDefinition.cs`、`StatusComponent.cs`

**建议方向：** 见 `Docs/13_FUTURE_DESIGN.md` §1 Status 系统设计

---

### IS-0002 AI Action Candidate 框架未实现

**严重级别：** P0（阻塞多功能敌方 AI）

**现状：**
- `EnemyTurnRunner.RunTurn()` 只接受单个 `SkillDefinition` 参数，执行"找最近玩家 → 移动 → 攻击"固定逻辑
- `AIProfile` 数据结构完整但**完全未被 EnemyTurnRunner 使用**
- `SkillPlan` 数据结构已定义但未被任何代码使用
- 不存在 `AIActionCandidate` 类型
- 不存在 `AIActionEvaluator` 评分/过滤/选择逻辑
- 敌方不支持技能选择、不支持治疗行为、不支持 buff 行为

**影响：**
- 所有敌方都是"追最近玩家 + 普攻"，无法区分 Aggressive/Support/Healer
- `AIProfile`、`aiTags`、`aiBaseWeight`、Tag 权重等配置完全白费
- 无法验证治疗型/支援型 AI 行为
- Docs/08 §7 的 8 步详细设计尚未落地

**涉及文件：**
- `Assets/Scripts/Combat/EnemyTurnRunner.cs`
- `Assets/Scripts/Combat/AI/AIProfile.cs`
- `Assets/Scripts/Combat/Skills/SkillPlan.cs`

**建议方向：** 见 `Docs/13_FUTURE_DESIGN.md` §2 AI 候选系统设计

---

### IS-0003 CombatRoundManager 职责过重

**严重级别：** P0（影响可维护性和扩展性）

**现状：**
`CombatRoundManager` 当前承担了以下所有职责（一个 ~380 行的类）：
1. 回合排序与状态机
2. 可控块计算
3. 技能资产加载（`ResolveBasicAttackSkill` / `ResolveBasicMoveSkill`）
4. 技能激活（选中角色时自动激活 basic_move）
5. 输入分发（`HandleInputReceived` → SkillExecutor）
6. 相机聚焦
7. 单位缓存（`CacheUnits` 通过 FindObjectsOfType）
8. 单位组件校验（`ValidateUnitSkillComponents`）
9. 回合结束处理
10. 敌方回合计时器

**问题：**
- 违反单一职责原则
- 技能资产加载使用 `#if UNITY_EDITOR` + `AssetDatabase.LoadAssetAtPath`（编辑器专用 API 出现在运行时代码中）
- 技能激活逻辑（`TrySelectPlayer` 中激活 basic_move）耦合在回合管理器中
- 输入分发逻辑混合了玩家切换（点击玩家 = 选择）和技能输入（点击地面 = 移动）

**涉及文件：**
- `Assets/Scripts/Combat/CombatRoundManager.cs`

**建议方向：** 
- 拆分出 `SkillActivationService`（技能激活/切换管理）
- 拆分出 `UnitRegistry`（单位注册/发现/缓存）
- 技能资产加载改为 Resources.Load 或 Addressables
- 输入分发中玩家选择逻辑独立为 `PlayerSelectionHandler`

---

### IS-0004 SkillDefinition.Ability 的 DefaultInstance 运行时构造

**严重级别：** P0（破坏 SO 资产约定）

**现状：**
```csharp
// SkillDefinition.cs:65-66
return m_targetType == ESkillTargetType.GroundPoint
    ? GroundMoveAbility.DefaultInstance : null;
```

`GroundMoveAbility.DefaultInstance` 通过 `CreateInstance<GroundMoveAbility>()` 在运行时动态创建 ScriptableObject，设置 `hideFlags = HideAndDontSave`。

**问题：**
1. ScriptableObject 设计为在编辑器中创建的持久化资产，不是运行时 new 出来的对象
2. `CreateInstance` 在非 Editor 环境下可能行为不一致
3. `HideAndDontSave` 意味着每次 Domain Reload 都会重新创建
4. Todo 注释说应该通过 Unity Editor 将 ability asset 挂到 `basic_move.asset` 上

**涉及文件：**
- `Assets/Scripts/Combat/Skills/SkillDefinition.cs:63-66`
- `Assets/Scripts/Combat/Skills/GroundMoveAbility.cs:12-24`

**建议方向：**
- 确保所有 `basic_move` 技能资产通过 Unity Editor 显式挂载 `GroundMoveAbility` 资产
- 移除 `DefaultInstance` 属性和相关 fallback 逻辑
- 在 Config Validation 中增加检查：`GroundPoint` 类型的技能必须配置 Ability

---

## P1 — 代码质量与兼容性

### IS-0005 EnemyTurnRunner 新旧 API 桥接冗余

**严重级别：** P1

**现状：**
`EnemyTurnRunner` 中的 bridge helpers 同时检查两种 API：
```csharp
// 例如 IsAlive
private static bool IsAlive(EnemyController enemy, AttributeSet attr)
{
    return attr != null ? attr.IsAlive : enemy.IsAlive;
}
// 例如 IsMovingEnemy
private static bool IsMovingEnemy(EnemyController enemy, MovementController move)
{
    return move != null ? move.IsMoving : enemy.IsMoving;
}
```

这些方法是 `AttributeSet` / `MovementController` 迁移过渡期的产物。现在迁移已完成，这些 fallback 分支的 legacy 路径（`enemy.IsAlive`、`enemy.IsMoving` 等）永远不会被触发（因为 `EnemyController` 的 `IsAlive` / `IsMoving` 属性内部也是转发到 `AttributeSet` / `MovementController`）。

**影响：**
- 代码膨胀（每处 1 行变 3-6 行）
- 新开发者误解为"两种 API 都可以用"
- 所有 `EnemyController` 的 pass-through 属性（CurrentAP, CurrentHP, IsMoving, IsAlive 等）都变成了仅用于 bridge 的冗余代码

**涉及文件：**
- `Assets/Scripts/Combat/EnemyTurnRunner.cs:284-315`
- `Assets/Scripts/Combat/EnemyController.cs:29-38`
- `Assets/Scripts/Combat/Player1Controller.cs:37-46`

**建议方向：**
- 清理所有 bridge helpers，直接使用 `AttributeSet` / `MovementController` API
- 逐步移除 `EnemyController` / `Player1Controller` 中的 pass-through 属性
- 外部代码直接通过 `GetComponent<AttributeSet>()` 访问属性

---

### IS-0006 CombatRoundManager 中编辑器代码在运行时路径

**严重级别：** P1

**现状：**
```csharp
// CombatRoundManager.cs:73-76
private void ResolveBasicAttackSkill()
{
    if (m_basicAttackSkillOverride != null) { ... return; }
#if UNITY_EDITOR
    BasicAttackSkill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>(
        "Assets/Data/Skills/basic_attack.asset");
    if (BasicAttackSkill != null) return;
#endif
    BasicAttackSkill = Resources.Load<SkillDefinition>("Skills/basic_attack");
    ...
}
```

`AssetDatabase.LoadAssetAtPath` 只在 Unity Editor 中可用。Build 后的运行时只会走 `Resources.Load` 路径。这意味着 Editor 和 Build 的资产加载路径不一致，可能在 Build 中出现 Editor 中不存在的 bug。

**涉及文件：**
- `Assets/Scripts/Combat/CombatRoundManager.cs:70-94`

**建议方向：**
- 移除 `#if UNITY_EDITOR` 分支
- 统一使用 `Resources.Load` 或使用 `m_basicAttackSkillOverride` 字段由 Inspector 显式配置
- 或将技能资产移到 `Resources/` 目录下

---

### IS-0007 SkillExecutor.CollectTags 手动 Faction 同步

**严重级别：** P1

**现状：**
```csharp
// SkillExecutor.cs:377-382
var attr = obj.GetComponent<AttributeSet>();
if (attr != null)
{
    var factionTag = new GameplayTag(attr.Faction == EFaction.Player
        ? "Faction.Player" : "Faction.Enemy");
    outTags.Add(factionTag, "SkillExecutor.FactionAutoSync");
}
```

`AttributeSet.Awake()` 中已经调用了 `SyncFactionTag()` 自动将 Faction 同步到 `GameplayTagComponent`。但 `SkillExecutor.CollectTags` 又手动做了一次同样的同步，使用字符串来源标识 `"SkillExecutor.FactionAutoSync"`。

**问题：**
- 重复逻辑，Faction Tag 被两个地方独立添加
- 如果 AttributeSet 的 Faction 改变（虽然当前不支持运行时切换），两处同步可能不一致
- 字符串来源标识散落在代码中不便于追踪

**涉及文件：**
- `Assets/Scripts/Combat/Skills/SkillExecutor.cs:365-382`

**建议方向：**
- 移除 `CollectTags` 中的手动 Faction 同步
- 依赖 `AttributeSet.SyncFactionTag()` 的唯一同步点
- 或将来源标识统一管理为常量

---

### IS-0008 EnemySpawner 使用临时 API

**严重级别：** P1

**现状：**
```csharp
// EnemySpawner.cs:46-51
var attr = go.AddComponent<AttributeSet>();
attr.Testing_AddAttribute(WellKnownAttributeTags.HP, m_hp, m_hp);
attr.Testing_AddAttribute(WellKnownAttributeTags.AP, 6f, 6f);
attr.Testing_AddAttribute(WellKnownAttributeTags.Initiative, m_initiative, 0f);
attr.Testing_AddAttribute(WellKnownAttributeTags.MoveSpeed, 2f, 0f);
attr.OverrideFactionForTesting(EFaction.Enemy);
```

`Testing_AddAttribute` 和 `OverrideFactionForTesting` 都被 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 包围。

**问题：**
- `EnemySpawner` 本身标记为临时占位（Docs/04_MONSTER_SPEC.md §5），但当前是主要敌人生成方式
- 使用 Testing API 而非正式 API（AttributeSetDef）
- Build 中这些方法不存在，EnemySpawner 无法正常工作

**涉及文件：**
- `Assets/Scripts/Combat/EnemySpawner.cs`
- `Assets/Scripts/Combat/AttributeSet.cs:173-179`

**建议方向：**
- 创建正式的 Enemy 工厂，使用 `AttributeSetDef` ScriptableObject 初始化属性
- 或将 EnemySpawner 改为使用 `AttributeSetDef` 引用
- 或实现 `MonsterDefinition` ScriptableObject + 关卡数据预放置

---

## P2 — 功能缺口

### IS-0009 CombatTrigger / 战斗进入流程缺失

**严重级别：** P2

**现状：**
- 不存在 `CombatTrigger` 组件
- 不存在 `VictoryConditionEvaluator`
- `CombatRoundManager.StartCombat()` 在 `Start()` 中直接调用
- 战斗状态机只实现了 CombatStart → TurnLoop → NextRound 循环
- 没有 Exploration → CombatStart 的过渡
- 没有 CombatEnd → Exploration 的返回

**影响：**
- 战斗无法"触发"，只能场景加载后自动开始
- 无法定义战斗区域/触发条件
- 无法处理胜利/失败/逃跑

**涉及文件：**
- `Assets/Scripts/Combat/CombatRoundManager.cs`
- 待创建：`CombatTrigger.cs`、`VictoryConditionEvaluator.cs`

**建议方向：** 见 `Docs/13_FUTURE_DESIGN.md` §3 战斗触发与流程设计

---

### IS-0010 配置校验不完整

**严重级别：** P2

**现状：**
Docs/08 §8.5 和 §1.5 提到了配置校验和编辑器窗口，但当前：
- `CombatConfigWindow` EditorWindow 的 Tags / Validation 页面尚未完整实现
- 没有运行时启动校验（检查所有单位是否有 SkillExecutor、是否有技能配置等）
- `CombatRoundManager.ValidateUnitSkillComponents()` 只做基本的警告输出

**缺失的校验项：**
- SkillDefinition.id 唯一性检查
- Effect 必须有至少一个 Tag
- GroundPoint 类型技能必须有 Ability
- AIProfile 引用完整性
- 单位必须配置 basic_move

**涉及文件：**
- 待实现完整校验工具

---

### IS-0011 冷却系统不支持探索模式

**严重级别：** P2

**现状：**
`SkillExecutor` 的冷却管理（`m_cooldowns`）只在 `AdvanceCooldowns()` 被调用时推进，这发生在新一轮的 `StartNextRound()` 中。按 Docs/05_SKILL_SPEC.md §6.2 的设计，探索模式下冷却应按秒刷新（`1 回合 = 6 秒`）。

**问题：**
- 当前没有探索模式，所以这不是阻塞性问题
- 但接口设计上没有给实时冷却留下扩展空间
- `AdvanceCooldowns()` 硬编码为每调用一次减 1

**涉及文件：**
- `Assets/Scripts/Combat/Skills/SkillExecutor.cs:194-203`

**建议方向：**
- 将冷却存储从 `int`（剩余回合数）改为支持两种模式
- 或引入 `ICooldownPolicy` 接口：`TurnBasedCooldownPolicy` / `RealTimeCooldownPolicy`

---

### IS-0012 TagRegistry 线性查找性能

**严重级别：** P2

**现状：**
```csharp
// TagRegistry.cs:19-31
public bool TryGetEntry(GameplayTag tag, out TagEntry entry)
{
    foreach (var e in m_entries)  // 线性遍历
    {
        if (e.Tag == tag) { ... }
    }
    ...
}
```

当 Tag 数量增长到 100+ 时，每次查询都是 O(n)。在有大量条件判断的战斗场景中可能成为性能热点。

**涉及文件：**
- `Assets/Scripts/GameplayTags/TagRegistry.cs`

**建议方向：**
- 内部使用 `Dictionary<GameplayTag, TagEntry>` 或 `Dictionary<int, TagEntry>`（按 hash）
- 保留 `List<TagEntry>` 用于编辑器显示排序

---

## P3 — 次要问题与改进建议

### IS-0013 GroundMoveAbility 中 NavMesh 采样重复调用

**现状：**
`GroundMoveAbility.TryBuildMovePath` 中先采样 NavMesh（`NavMesh.SamplePosition`），然后用完整路径计算（`NavMesh.CalculatePath`），路径超长时做 `PathCostCalculator.Clip`，再用裁剪后的终点重新 `NavMesh.CalculatePath`。总共最多 3 次 NavMesh 查询。

**影响：** 每帧 hover 预览时多余的 NavMesh 开销。

**涉及文件：**
- `Assets/Scripts/Combat/Skills/GroundMoveAbility.cs:148-191`

---

### IS-0014 EffectDefinition.HasAnyTag vs 空 Tag 检查不一致

**现状：**
`EffectDefinition.HasAnyTag()` 检查 `!string.IsNullOrEmpty(all[i].Value)`。但在 `TagQuery.Evaluate` 中空 Tag 直接跳过。两者对"空 Tag"的处理方式一致但分布在两处，应统一到 `GameplayTag.IsValid()`。

**涉及文件：**
- `Assets/Scripts/Combat/Skills/EffectDefinition.cs:17-23`
- `Assets/Scripts/GameplayTags/TagQuery.cs:47,55,63`

---

### IS-0015 CombatRoundManager.m_turnOrder 使用 GameObject 而非组件引用

**现状：**
`m_turnOrder` 是 `List<GameObject>`，访问属性时反复 `GetComponent<AttributeSet>()`、`GetComponent<Player1Controller>()`、`GetComponent<EnemyController>()`。这些 `GetComponent` 调用分散在 `BuildTurnOrder`、`RefreshControllableBlock`、`AdvanceTurn`、`StartNextRound`、`EnemyTurnCoroutine` 等多个方法中。

**涉及文件：**
- `Assets/Scripts/Combat/CombatRoundManager.cs`

---

### IS-0016 缺少战斗日志/事件总线

**现状：**
战斗中的关键事件（技能施放、伤害、治疗、死亡、回合切换）通过 `Debug.Log` 输出，但没有统一的事件系统供 UI、音效、VFX、成就等系统订阅。

**影响：**
- 后续 UI 更新需要各自轮询状态
- VFX / 音效无法解耦触发

**建议方向：** 见 `Docs/13_FUTURE_DESIGN.md` §4 事件系统设计

---

### IS-0017 Player1Controller.OnAttributeDepleted 为空实现

**现状：**
```csharp
// Player1Controller.cs:164
private void OnAttributeDepleted(GameplayTags.GameplayTag tag) { }
```

敌方 HP 归零时自动 `Destroy(gameObject)`（`EnemyController.OnAttributeDepleted`），但玩家 HP 归零时没有任何处理。

**涉及文件：**
- `Assets/Scripts/Combat/Player1Controller.cs:164`

---

### IS-0018 APDebugHUD 直接访问内部字段

**现状：**
`APDebugHUD.OnGUI()` 中频繁使用 `GetComponent<AttributeSet>()` 和各种 pass-through 属性，HUD 代码和角色实现耦合紧密。

**涉及文件：**
- `Assets/Scripts/Combat/Debug/APDebugHUD.cs`

---

## 2026-05-11 分支审阅新增问题

> 来源：`refactor/combat-round-enemy-ai` 分支审阅。`m_maxPartySize` 不再作为硬性限制，允许玩家单位数量突破 4，因此不记录为问题。

### IS-0019 场景未迁移到 CombatUnit / UnitTurnHandler / EnemyTurnRunner 新架构

**严重级别：** P0（阻塞 Play 模式回合闭环）

**现状：**
- `CombatRoundManager.CollectUnits()` 现在只通过 `FindObjectsOfType<CombatUnit>()` 收集参战单位。
- 当前 `SampleScene` 中玩家和敌人对象仍只挂载旧组件栈，未发现 `CombatUnit` 组件引用。
- 当前 `SampleScene` 的 `[CombatRoundManager]` 对象仍只挂载 `CombatRoundManager`，没有挂载或配置新增的 `UnitTurnHandler` / `EnemyTurnRunner`。
- 新增 `UnitTurnHandler.cs.meta` 当前处于未跟踪状态，场景也没有引用该脚本。

**影响：**
- `StartCombat()` 收集到 0 个单位，回合队列为空。
- `UnitTurnStarted` 没有玩家侧或敌方侧处理器订阅，输入、相机聚焦、敌方 AI 都不会进入新流程。
- 分支代码和场景资产状态不一致，Play 模式无法验证新回合架构。

**涉及文件：**
- `Assets/Scripts/Combat/CombatRoundManager.cs`
- `Assets/Scripts/Combat/CombatUnit.cs`
- `Assets/Scripts/Combat/UnitTurnHandler.cs`
- `Assets/Scripts/Combat/EnemyTurnRunner.cs`
- `Assets/Scenes/SampleScene.unity`

**建议方向：**
- 通过 Unity Editor / Unity Skills 为所有参战单位显式添加 `CombatUnit`。
- 在 `Systems/[CombatSystems]` 或当前战斗系统对象上挂载并配置 `UnitTurnHandler` / `EnemyTurnRunner`。
- 将 `m_roundManager`、`m_inputController`、`m_cameraController` 等引用通过 Inspector 显式拖入；`FindObjectOfType` 只保留 Awake fallback 并输出警告。
- 确认并提交 Unity 生成的完整 `.meta` 文件。

---

### IS-0020 回合开始 AP 恢复与移动预算重置丢失

**严重级别：** P0（破坏 AP / 移动核心规则）

**现状：**
- 旧版 `CombatRoundManager.StartNextRound()` 会对每个存活单位执行 `AttributeSet.SetToMax(AP)`、`MovementController.ResetUnpaidDistance()` 和 `SkillExecutor.AdvanceCooldowns()`。
- 新版 `StartNextRound()` 只调用 `SkillExecutor.OnRoundStart()`。
- 当前 `SkillExecutor.OnRoundStart()` 只推进冷却、tick/expire active effects，没有恢复 AP，也没有重置 `MovementController` 的 unpaid movement distance。
- 文档设计中的 `RoundPhaseManager`、`RestoreAttributeEffect`、`ResetMovementEffect`、系统技能 `sys_round_start` 目前尚未落地到代码和资产。

**影响：**
- 第一轮消耗 AP 后，下一轮不会恢复 AP。
- `m_unpaidMoveDistance` 跨轮残留，导致 `canWalkDistance = CurrentAP * MoveSpeed` 的预算计算被污染。
- `State.APBlocked` / `State.Rooted` 这类基于系统技能和 Tag 条件的阻断设计无法验证。

**涉及文件：**
- `Assets/Scripts/Combat/CombatRoundManager.cs`
- `Assets/Scripts/Combat/Skills/SkillExecutor.cs`
- `Assets/Scripts/Combat/MovementController.cs`
- 待创建：`RoundPhaseManager.cs`
- 待创建：`RestoreAttributeEffectDefinition.cs`
- 待创建：`ResetMovementEffectDefinition.cs`

**建议方向：**
- 按 `Docs/14_ROUND_EVENT_SYSTEM.md` 实现 `RoundPhaseManager` 与系统技能。
- 在 `RoundStarted` 时通过显式配置的 `sys_round_start` 技能恢复 AP、重置移动预算、推进冷却。
- 在系统技能落地前，如需保持原型可跑，需要明确临时恢复路径并加 TODO 引用本条 issue。

---

### IS-0021 玩家点击选择未校验 ControllableUnits

**严重级别：** P1（可破坏连续玩家块先攻规则）

**现状：**
- `UnitTurnHandler.Update()` 中数字键切换使用 `m_roundManager.ControllableUnits`。
- `UnitTurnHandler.OnInputReceived()` 中点击玩家对象时直接调用 `SelectUnit(player.gameObject)`。
- `SelectUnit()` 本身不检查目标是否属于当前 `ControllableUnits`，也不检查目标是否已经结束本轮行动。

**影响：**
- 在 `P1 -> Enemy -> P2` 的 turn order 中，玩家可能通过点击提前选择并控制 P2。
- 违反 `OPEN_QUESTIONS.md` 中 `Q-0024` 已确认的"连续玩家块内自由切换"规则。

**涉及文件：**
- `Assets/Scripts/Combat/UnitTurnHandler.cs`
- `Docs/OPEN_QUESTIONS.md` (`Q-0024`)

**建议方向：**
- 增加 `TrySelectUnit(GameObject unit)`，统一检查 `ControllableUnits.Contains(unit)` 和 `!HasEndedRound(unit)`。
- 数字键与点击选择都走同一个选择入口。
- 对非法点击仅忽略或给 debug 日志，不把选择状态切出当前可控块。

---

### IS-0022 UnitTurnHandler.cs.meta 未完整提交

**严重级别：** P2（资产导入稳定性）

**现状：**
- 工作区存在未跟踪的 `Assets/Scripts/Combat/UnitTurnHandler.cs.meta`。
- 当前文件内容只有 `fileFormatVersion` 和 `guid`，缺少 Unity 常规 `MonoImporter` 块。
- `UnitTurnHandler.cs` 已纳入分支代码，但对应 `.meta` 未进入 git。

**影响：**
- 不同机器或 Unity 重新导入时可能生成不同 GUID。
- 后续一旦场景或 prefab 引用 `UnitTurnHandler`，缺失/变化的 GUID 会造成 Missing Script 风险。

**涉及文件：**
- `Assets/Scripts/Combat/UnitTurnHandler.cs`
- `Assets/Scripts/Combat/UnitTurnHandler.cs.meta`

**建议方向：**
- 让 Unity 重新导入该脚本并生成完整 `.meta`。
- 确认 `.meta` 包含 `MonoImporter` 后提交。

---

## 问题状态汇总

| ID | 标题 | 严重级别 | 状态 |
|----|------|---------|------|
| IS-0001 | Status 系统完全缺失 | P0 | 待处理 |
| IS-0002 | AI Action Candidate 框架未实现 | P0 | 待处理 |
| IS-0003 | CombatRoundManager 职责过重 | P0 | 待处理 |
| IS-0004 | SkillDefinition.Ability DefaultInstance 运行时构造 | P0 | 待处理 |
| IS-0005 | EnemyTurnRunner 新旧 API 桥接冗余 | P1 | 待处理 |
| IS-0006 | CombatRoundManager 编辑器代码在运行时路径 | P1 | 待处理 |
| IS-0007 | SkillExecutor.CollectTags 手动 Faction 同步重复 | P1 | 待处理 |
| IS-0008 | EnemySpawner 使用临时 API | P1 | 待处理 |
| IS-0009 | CombatTrigger / 战斗进入流程缺失 | P2 | 待处理 |
| IS-0010 | 配置校验不完整 | P2 | 待处理 |
| IS-0011 | 冷却系统不支持探索模式 | P2 | 待处理 |
| IS-0012 | TagRegistry 线性查找性能 | P2 | 待处理 |
| IS-0013 | GroundMoveAbility NavMesh 重复采样 | P3 | 待处理 |
| IS-0014 | EffectDefinition 空 Tag 检查不一致 | P3 | 待处理 |
| IS-0015 | turnOrder 使用 GameObject 而非组件引用 | P3 | 待处理 |
| IS-0016 | 缺少战斗日志/事件总线 | P3 | 待处理 |
| IS-0017 | Player1Controller.OnAttributeDepleted 为空 | P3 | 待处理 |
| IS-0018 | APDebugHUD 直接访问内部字段 | P3 | 待处理 |
| IS-0019 | 场景未迁移到 CombatUnit / UnitTurnHandler / EnemyTurnRunner 新架构 | P0 | 待处理 |
| IS-0020 | 回合开始 AP 恢复与移动预算重置丢失 | P0 | 待处理 |
| IS-0021 | 玩家点击选择未校验 ControllableUnits | P1 | 待处理 |
| IS-0022 | UnitTurnHandler.cs.meta 未完整提交 | P2 | 待处理 |

# 14 - 回合事件驱动 + 系统技能架构

> 2026-05-11 设计。将 CombatRoundManager 从"上帝对象"重构为纯回合事件广播器，所有回合维护操作（恢复 AP、推进冷却、Status tick、重置移动预算等）改为通过系统技能执行。

## 1. 核心思想

```
之前：
  CombatRoundManager.StartNextRound()
    ├── foreach unit: attr.SetToMax(AP)       ← 硬编码
    ├── foreach unit: movement.ResetUnpaid()  ← 硬编码
    └── foreach unit: executor.AdvanceCooldowns() ← 硬编码

之后：
  RoundPhaseManager 收到 RoundStarted 事件
    └── foreach unit:
          SkillExecutor.Execute(sys_round_start, unit)
            ├── RestoreAPEffect         ← 可被 State.APBlocked Tag 阻断
            ├── ResetMovementEffect     ← 可被 State.Rooted Tag 阻断
            ├── AdvanceCooldownsEffect
            └── TriggerStatusTurnStart  ← 触发 DOT/HoT/buff tick
```

**核心原则：CombatRoundManager 只负责回合队列，不执行任何游戏逻辑。所有游戏逻辑都是技能。**

## 2. 架构对比

```
╔══════════════════════ 之前 ═══════════════════════╗
║ CombatRoundManager (380行 God Object)            ║
║ ├── 回合排序                                      ║
║ ├── 硬编码 AP 恢复                                ║
║ ├── 硬编码冷却推进                                ║
║ ├── 硬编码移动重置                                ║
║ ├── 技能资产加载 (#if UNITY_EDITOR + Resources)   ║
║ ├── 玩家选中 + 技能激活                           ║
║ ├── 输入路由                                      ║
║ ├── 敌方回合协程                                  ║
║ ├── 相机聚焦                                      ║
║ └── Update() 按键轮询                             ║
╚════════════════════════════════════════════════════╝

╔══════════════════════ 之后 ═══════════════════════╗
║ CombatRoundManager (~120行)                       ║
║ ├── 单位收集 + 先攻排序                           ║
║ ├── 可控块计算                                    ║
║ ├── 广播事件: RoundStarted / UnitTurnStarted       ║
║ └── 接收 EndTurn → AdvanceToNext                  ║
║                                                   ║
║ RoundPhaseManager (~80行)                         ║
║ ├── [SerializeField] SkillDefinition[] m_roundStartSkills ← Inspector显式配置  ║
║ ├── [SerializeField] SkillDefinition[] m_turnEndSkills    ← Inspector显式配置  ║
║ ├── 订阅 RoundStarted                             ║
║ └── 订阅 UnitTurnEnded                            ║
║                                                   ║
║ UnitTurnHandler (~100行) — 玩家侧                 ║
║ ├── [SerializeField] CombatRoundManager m_roundManager ← Inspector拖入  ║
║ ├── [SerializeField] InputController m_inputController  ← Inspector拖入  ║
║ ├── [SerializeField] CameraController m_camera          ← Inspector拖入  ║
║ ├── 订阅 UnitTurnStarted (Faction=Player)         ║
║ ├── 选中管理 + 数字键切换                         ║
║ └── Space → m_roundManager.EndTurn                ║
║                                                   ║
║ AITurnRunner (~100行) — 敌方侧                    ║
║ ├── [SerializeField] CombatRoundManager m_roundManager ← Inspector拖入  ║
║ ├── [SerializeField] CameraController m_camera          ← Inspector拖入  ║
║ ├── 订阅 UnitTurnStarted (Faction=Enemy)          ║
║ ├── 从单位自身 SkillExecutor.availableSkills 获取  ║
║ └── AIActionEvaluator → m_roundManager.EndTurn     ║
╚════════════════════════════════════════════════════╝
```

**所有资产引用通过 Inspector 显式配置，不使用 Resources.Load / AssetDatabase / FindObjectOfType（仅 Awake 中作 fallback + 警告）。**

## 2.5 场景配置方式

### 场景层级

```
SampleScene
├── Systems/
│   └── [CombatSystems]                          ← 挂载所有战斗管理器
│       ├── CombatRoundManager (MB)               ← 回合状态机
│       │   └── (Inspector: enemyFirst=false)
│       │
│       ├── RoundPhaseManager (MB)                ← 回合事件 → 系统技能
│       │   ├── m_roundStartSkills: [sys_round_start]           ← Inspector拖入
│       │   ├── m_turnEndSkills: [sys_turn_end]                 ← Inspector拖入
│       │   └── m_roundManager: → CombatRoundManager            ← Inspector拖入
│       │
│       ├── UnitTurnHandler (MB)                  ← 玩家回合处理
│       │   ├── m_roundManager: → CombatRoundManager            ← Inspector拖入
│       │   ├── m_inputController: → InputController            ← Inspector拖入
│       │   └── m_cameraController: → CameraController          ← Inspector拖入
│       │
│       ├── AITurnRunner (MB)                     ← 敌方AI回合
│       │   ├── m_roundManager: → CombatRoundManager            ← Inspector拖入
│       │   └── m_cameraController: → CameraController          ← Inspector拖入
│       │
│       ├── InputController (MB)                  ← 输入
│       ├── CameraController (MB)                 ← 相机
│       ├── NavMeshManager (MB)                   ← NavMesh配置
│       ├── PathPreview (MB)                      ← 路径预览
│       └── CombatEventBus (MB)                   ← 事件总线
│
├── Gameplay/
│   └── Actors/
│       ├── Player_Warrior                        ← 场景预置单位
│       │   ├── CombatUnit (MB)                   ← 标记为参战单位
│       │   ├── AttributeSet (MB)
│       │   │   └── m_definition: Warrior_Def     ← Inspector拖入AttributeSetDef
│       │   ├── MovementController (MB)
│       │   ├── SkillExecutor (MB)
│       │   │   └── m_availableSkills: [basic_move, basic_attack, power_strike] ← Inspector拖入
│       │   ├── GameplayTagComponent (MB)
│       │   └── Player1Controller (MB)            ← 视觉 + UI
│       │
│       ├── Enemy_Goblin_Melee                    ← 场景预置单位
│       │   ├── CombatUnit (MB)
│       │   ├── AttributeSet (MB)
│       │   │   └── m_definition: Goblin_Melee_Def ← AttributeSetDef资产
│       │   ├── MovementController (MB)
│       │   ├── SkillExecutor (MB)
│       │   │   └── m_availableSkills: [basic_move, basic_attack]  ← Inspector拖入
│       │   ├── GameplayTagComponent (MB)
│       │   └── EnemyController (MB)
│       │       └── m_aiProfile: Aggressive_Profile  ← Inspector拖入
│       │
│       └── Enemy_Shaman_Support                  ← 场景预置单位
│           ├── ... (同上)
│           └── EnemyController (MB)
│               └── m_aiProfile: Support_Profile  ← 支援型AI
```

### 关键原则

| 原则 | 说明 |
|------|------|
| **资产全部拖入 Inspector** | 技能列表、系统技能、AIProfile、AttributeSetDef 全部通过 `[SerializeField]` 在场景或 Prefab 上显式配置 |
| **不使用字符串路径加载** | 不写 `Resources.Load("Skills/basic_attack")`，不写 `AssetDatabase.LoadAssetAtPath` |
| **管理器间显式引用** | `UnitTurnHandler` 需要 `CombatRoundManager`？→ Inspector 拖入，不写 `FindObjectOfType`（Awake 中仅做 fallback + 警告） |
| **单位自带技能配置** | 每个单位的 `SkillExecutor.availableSkills[]` 在 Inspector 中独立配置，不依赖全局管理器补发技能 |
| **后期统一面板** | 当前手动在每个对象上配置；后期所有配置入口统一到 `MiniChess/Combat Config` 编辑器窗口 |

## 3. 回合生命周期事件

```
CombatStart
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│ RoundStart                                                   │
│   │                                                          │
│   ├── CombatRoundManager: 收集单位、排序、构建 turn order    │
│   │                                                          │
│   ├── 广播: RoundPhase.RoundStart                            │
│   │   └── RoundPhaseManager:                                 │
│   │       foreach alive unit:                                │
│   │         SkillExecutor.Execute(sys_round_start)           │
│   │           ├── RestoreAPEffect                            │
│   │           │   └── Tag条件: blockedBy=[State.APBlocked]   │
│   │           ├── ResetMovementEffect                        │
│   │           │   └── Tag条件: blockedBy=[State.Rooted]      │
│   │           ├── AdvanceCooldownsEffect                     │
│   │           └── TriggerStatusTurnStartEffect               │
│   │               └── 遍历 StatusComponent 的 OnTurnStart    │
│   │                                                          │
│   ├── 广播: RoundStarted(RoundCount)                         │
│   │                                                          │
│   └── AdvanceToNextUnit ──────────────────────┐              │
└────────────────────────────────────────────────┼──────────────┘
                                                 │
                    ┌────────────────────────────┘
                    ▼
┌──────────────────────────────────────────────────────────────┐
│ UnitTurnStarted(unit)                                        │
│   │                                                          │
│   ├── Faction = Player?                                      │
│   │   └── UnitTurnHandler: 进入玩家操作模式                   │
│   │       ├── 相机聚焦                                       │
│   │       ├── 激活可用技能                                    │
│   │       └── 等待输入...                                    │
│   │                                                          │
│   ├── Faction = Enemy?                                       │
│   │   └── AITurnRunner: 启动 AI 协程                          │
│   │       ├── 设置 IsWaiting = true                           │
│   │       ├── 相机聚焦                                       │
│   │       ├── AIActionEvaluator.Evaluate(unit, ...)           │
│   │       ├── 执行移动 + 技能                                 │
│   │       └── CombatRoundManager.EndTurn(unit)               │
│   │                                                          │
│   └── CombatRoundManager.EndTurn(unit)                       │
│       ├── 标记 hasEndedRound                                 │
│       ├── 广播: UnitTurnEnded(unit)  ← 可选，预留             │
│       │   └── RoundPhaseManager:                              │
│       │       SkillExecutor.Execute(sys_turn_end, unit)      │
│       │         ├── TriggerStatusTurnEndEffect               │
│       │         └── DecrementStatusDurationEffect            │
│       │                                                      │
│       └── AdvanceToNextUnit ──────────────┐                  │
└────────────────────────────────────────────┼──────────────────┘
                                             │
                              ┌──────────────┘
                              ▼
                        队列空? → RoundStart (下一轮)
```

## 4. 系统技能设计

系统技能是普通的 `SkillDefinition` 资产，区别在于：
- 它们不通过玩家 UI 选择，由 `RoundPhaseManager` 自动调用
- `targetType = Self`（对单位自身生效）
- `apCost = 0`（系统执行，不消耗 AP）
- 通过 Tag 条件实现 debuff 阻断

### 4.1 sys_round_start

```
SkillDefinition: sys_round_start
├── id: "sys_round_start"
├── displayName: "回合开始维护"
├── apCost: 0
├── targetType: Self
├── effects:
│   ├── RestoreAttributeEffect
│   │   ├── tag: Attribute.AP
│   │   ├── mode: ToMax
│   │   └── tags: [Effect.System.APRestore]
│   │
│   ├── ResetMovementEffect
│   │   └── tags: [Effect.System.MovementReset]
│   │
│   ├── AdvanceCooldownsEffect
│   │   └── tags: [Effect.System.CooldownAdvance]
│   │
│   └── TriggerStatusTickEffect
│       ├── phase: TurnStart
│       └── tags: [Effect.System.StatusTick]
│
├── blockedTargetTags: (系统技能 target=Self，所以用 blockedCasterTags 代替)
│   (实际执行时 caster 和 target 是同一单位，两边 Tag 条件都生效)
│
└── (aiTags 不需要，系统技能不会被 AI 评估)
```

### 4.2 sys_turn_end

```
SkillDefinition: sys_turn_end
├── id: "sys_turn_end"
├── apCost: 0
├── targetType: Self
├── effects:
│   ├── TriggerStatusTickEffect
│   │   └── phase: TurnEnd
│   │
│   └── DecrementStatusDurationEffect
│       └── (所有 Status 的 remainingRounds -= 1)
```

### 4.3 如何实现 debuff "阻止 AP 恢复"

假设一个 debuff 技能 `crippling_hex`：

```
1. crippling_hex 的 AddStatusEffect 添加 Status: hex_ap_block
2. hex_ap_block 的 appliedTags 包含 [State.APBlocked]
3. sys_round_start 的 RestoreAttributeEffect 配置:
   blockedTargetTags: [State.APBlocked]
4. 执行 sys_round_start 时:
   EvaluateTagConditions() → 发现目标有 State.APBlocked → RestoreAP 效果被跳过
5. 其他效果 (ResetMovement, AdvanceCooldowns) 不受影响
```

**不需要写一行特殊代码。**全部通过 Tag 条件配置实现。

## 5. 新增 Effect 类型

### 5.1 RestoreAttributeEffect

```csharp
[CreateAssetMenu(menuName = "MiniChess/Effects/Restore Attribute")]
public class RestoreAttributeEffect : EffectDefinition
{
    [SerializeField] private GameplayTag m_attributeTag;  // e.g. Attribute.AP
    [SerializeField] private ERestoreMode m_mode = ERestoreMode.ToMax;
    [SerializeField] private float m_fixedAmount = 1f;    // 仅 FixedAmount 模式

    public override ETargetCapability RequiredCapability => ETargetCapability.None;

    public override void Apply(EffectContext context)
    {
        var attr = context.Target?.GetComponent<AttributeSet>();
        if (attr == null) return;

        switch (m_mode)
        {
            case ERestoreMode.ToMax:
                attr.SetToMax(m_attributeTag);
                break;
            case ERestoreMode.FixedAmount:
                attr.Modify(m_attributeTag, +m_fixedAmount);
                break;
            case ERestoreMode.PercentOfMax:
                float max = attr.GetMax(m_attributeTag);
                attr.Modify(m_attributeTag, max * m_fixedAmount);
                break;
        }
    }

    private enum ERestoreMode { ToMax, FixedAmount, PercentOfMax }
}
```

### 5.2 ResetMovementEffect

```csharp
[CreateAssetMenu(menuName = "MiniChess/Effects/Reset Movement")]
public class ResetMovementEffect : EffectDefinition
{
    public override ETargetCapability RequiredCapability => ETargetCapability.None;

    public override void Apply(EffectContext context)
    {
        context.Target?.GetComponent<MovementController>()?.ResetUnpaidDistance();
    }
}
```

### 5.3 AdvanceCooldownsEffect

```csharp
[CreateAssetMenu(menuName = "MiniChess/Effects/Advance Cooldowns")]
public class AdvanceCooldownsEffect : EffectDefinition
{
    public override ETargetCapability RequiredCapability => ETargetCapability.None;

    public override void Apply(EffectContext context)
    {
        context.Target?.GetComponent<SkillExecutor>()?.AdvanceCooldowns();
    }
}
```

### 5.4 TriggerStatusTickEffect

```csharp
[CreateAssetMenu(menuName = "MiniChess/Effects/Trigger Status Tick")]
public class TriggerStatusTickEffect : EffectDefinition
{
    [SerializeField] private EStatusTickPhase m_phase = EStatusTickPhase.TurnStart;

    public override ETargetCapability RequiredCapability => ETargetCapability.None;

    public override void Apply(EffectContext context)
    {
        var statusComp = context.Target?.GetComponent<StatusComponent>();
        if (statusComp == null) return;

        // 获取该单位的所有 Status，触发对应 phase 的 tick effects
        foreach (var status in statusComp.ActiveStatuses)
        {
            var tickEffects = m_phase == EStatusTickPhase.TurnStart
                ? status.Definition.OnTurnStartEffects
                : status.Definition.OnTurnEndEffects;

            foreach (var effect in tickEffects)
            {
                if (effect == null) continue;
                var tickContext = new EffectContext
                {
                    Caster = status.Source,       // 施加者的 GameObject
                    Target = context.Target,       // 自身
                    CasterExecutor = status.Source?.GetComponent<SkillExecutor>(),
                    TargetExecutor = context.TargetExecutor,
                };
                effect.Apply(tickContext);
            }
        }
    }

    private enum EStatusTickPhase { TurnStart, TurnEnd }
}
```

### 5.5 DecrementStatusDurationEffect

```csharp
[CreateAssetMenu(menuName = "MiniChess/Effects/Decrement Status Duration")]
public class DecrementStatusDurationEffect : EffectDefinition
{
    public override ETargetCapability RequiredCapability => ETargetCapability.None;

    public override void Apply(EffectContext context)
    {
        context.Target?.GetComponent<StatusComponent>()?.DecrementAllDurations();
    }
}
```

## 6. RoundPhaseManager 实现

```csharp
/// <summary>
/// 订阅 CombatRoundManager 的回合事件，对单位执行系统技能。
/// </summary>
public class RoundPhaseManager : MonoBehaviour
{
    [Header("System Skills")]
    [Tooltip("回合开始时对每个存活单位执行的技能")]
    [SerializeField] private SkillDefinition m_roundStartSkill;   // sys_round_start asset

    [Tooltip("单位回合结束时对该单位执行的技能")]
    [SerializeField] private SkillDefinition m_turnEndSkill;      // sys_turn_end asset

    [Header("Refs")]
    [SerializeField] private CombatRoundManager m_roundManager;

    private void Awake()
    {
        // m_roundManager 通过 Inspector 拖入 — 不使用 FindObjectOfType
    }

    private void OnEnable()
    {
        if (m_roundManager != null)
        {
            m_roundManager.RoundStarted += OnRoundStarted;
            m_roundManager.UnitTurnEnded += OnUnitTurnEnded;
        }
    }

    private void OnDisable()
    {
        if (m_roundManager != null)
        {
            m_roundManager.RoundStarted -= OnRoundStarted;
            m_roundManager.UnitTurnEnded -= OnUnitTurnEnded;
        }
    }

    private void OnRoundStarted(int roundCount)
    {
        if (m_roundStartSkill == null)
        {
            Debug.LogWarning("[RoundPhase] No sys_round_start skill assigned.");
            return;
        }

        // 对 turn order 中每个存活单位执行 sys_round_start
        foreach (var unit in m_roundManager.TurnOrder)
        {
            var attr = unit?.GetComponent<AttributeSet>();
            if (attr == null || !attr.IsAlive) continue;

            var executor = unit.GetComponent<SkillExecutor>();
            if (executor == null) continue;

            var result = executor.Execute(m_roundStartSkill, unit); // target = self
            if (!result.IsSuccess)
            {
                Debug.Log($"[RoundPhase] {attr.DisplayName}: "
                    + $"sys_round_start partially blocked — {result.FailureMessage}");
            }
        }
    }

    private void OnUnitTurnEnded(GameObject unit)
    {
        if (m_turnEndSkill == null) return;

        var attr = unit?.GetComponent<AttributeSet>();
        if (attr == null || !attr.IsAlive) return;

        var executor = unit.GetComponent<SkillExecutor>();
        executor?.Execute(m_turnEndSkill, unit);
    }
}
```

## 7. 精简后的 CombatRoundManager

```csharp
/// <summary>
/// 纯回合状态机。只负责:
/// 1. 收集参战单位、按先攻排序
/// 2. 维护 turn order 队列 + 可控块
/// 3. 广播事件通知"轮到谁"
/// 4. 接收 EndTurn 并推进队列
///
/// 不负责任何游戏逻辑（AP、技能、冷却、移动、AI、输入、相机）。
/// </summary>
public class CombatRoundManager : MonoBehaviour
{
    // ── 事件 ──

    /// <summary>新轮次开始（已排序、已构建 turn order）</summary>
    public event Action<int> RoundStarted;

    /// <summary>轮到指定单位行动</summary>
    public event Action<GameObject> UnitTurnStarted;

    /// <summary>单位结束行动（EndTurn 被调用后触发）</summary>
    public event Action<GameObject> UnitTurnEnded;

    /// <summary>战斗结束</summary>
    public event Action<EBattleResult> CombatEnded;

    // ── Inspector ──

    [SerializeField] private bool m_enemyFirstForDebug = false;

    // ── 状态 ──

    private readonly List<GameObject> m_turnOrder = new();
    private readonly List<GameObject> m_controllableUnits = new();
    private readonly HashSet<GameObject> m_hasEndedRound = new();

    public IReadOnlyList<GameObject> TurnOrder => m_turnOrder;
    public IReadOnlyList<GameObject> ControllableUnits => m_controllableUnits;
    public GameObject ActiveUnit { get; private set; }
    public int RoundCount { get; private set; }
    public bool IsWaiting { get; set; }

    // ── 公共入口 ──

    public void StartCombat()
    {
        var units = CollectUnits();
        BuildTurnOrder(units);
        RoundCount = 0;
        m_hasEndedRound.Clear();
        StartNextRound();
    }

    public bool EndTurn(GameObject unit)
    {
        if (unit == null || m_hasEndedRound.Contains(unit)) return false;

        var movement = unit.GetComponent<MovementController>();
        if (movement != null && movement.IsMoving) return false;

        m_hasEndedRound.Add(unit);
        UnitTurnEnded?.Invoke(unit);  // RoundPhaseManager 执行 sys_turn_end
        AdvanceToNextUnit();
        return true;
    }

    public bool HasEndedRound(GameObject unit) =>
        unit != null && m_hasEndedRound.Contains(unit);

    public bool IsInControllableBlock(GameObject unit) =>
        m_controllableUnits.Contains(unit);

    // ── 内部 ──

    private List<GameObject> CollectUnits()
    {
        var result = new List<GameObject>();
        foreach (var cu in FindObjectsOfType<CombatUnit>())
        {
            if (cu.gameObject.activeInHierarchy)
                result.Add(cu.gameObject);
        }
        return result;
    }

    private void BuildTurnOrder(List<GameObject> units)
    {
        m_turnOrder.Clear();
        m_turnOrder.AddRange(units
            .OrderBy(go => m_enemyFirstForDebug
                && GetFaction(go) == EFaction.Enemy ? 0 : 1)
            .ThenByDescending(go => GetInitiative(go))
            .ThenBy(go => go.name));
    }

    private void StartNextRound()
    {
        RoundCount++;
        var units = CollectUnits();
        BuildTurnOrder(units);
        m_hasEndedRound.Clear();

        // 广播 RoundStarted — RoundPhaseManager 会执行 sys_round_start
        RoundStarted?.Invoke(RoundCount);

        RefreshControllableBlock();
        AdvanceToNextUnit();
    }

    private void RefreshControllableBlock()
    {
        m_controllableUnits.Clear();
        if (m_turnOrder.Count == 0) return;

        var firstFaction = GetFaction(m_turnOrder[0]);
        if (firstFaction == null) return;

        foreach (var go in m_turnOrder)
        {
            if (go == null || !IsAlive(go) || m_hasEndedRound.Contains(go)) continue;
            if (GetFaction(go) != firstFaction) break;
            m_controllableUnits.Add(go);
        }
    }

    private void AdvanceToNextUnit()
    {
        // 清理队首无效单位
        while (m_turnOrder.Count > 0)
        {
            var front = m_turnOrder[0];
            if (front == null || !IsAlive(front) || m_hasEndedRound.Contains(front))
                m_turnOrder.RemoveAt(0);
            else
                break;
        }

        if (m_turnOrder.Count == 0)
        {
            CheckVictory();
            return;
        }

        RefreshControllableBlock();

        // 可控块全空 → 跳过
        if (m_controllableUnits.Count == 0)
        {
            foreach (var go in m_turnOrder.ToList())
                m_turnOrder.Remove(go);
            AdvanceToNextUnit();
            return;
        }

        UnitTurnStarted?.Invoke(m_controllableUnits[0]);
        ActiveUnit = m_controllableUnits[0];
    }

    private void CheckVictory()
    {
        bool anyPlayerAlive = false;
        bool anyEnemyAlive = false;

        foreach (var unit in CollectUnits())
        {
            var attr = unit.GetComponent<AttributeSet>();
            if (attr == null || !attr.IsAlive) continue;
            if (attr.Faction == EFaction.Player) anyPlayerAlive = true;
            if (attr.Faction == EFaction.Enemy) anyEnemyAlive = true;
        }

        if (!anyPlayerAlive) CombatEnded?.Invoke(EBattleResult.Defeat);
        else if (!anyEnemyAlive) CombatEnded?.Invoke(EBattleResult.Victory);
        else StartNextRound();
    }

    // ── Helpers ──

    private static bool IsAlive(GameObject go) =>
        go.GetComponent<AttributeSet>()?.IsAlive ?? false;

    private static float GetInitiative(GameObject go) =>
        go.GetComponent<AttributeSet>()?.Get(WellKnownAttributeTags.Initiative) ?? 0f;

    private static EFaction? GetFaction(GameObject go) =>
        go.GetComponent<AttributeSet>()?.Faction;
}
```

## 8. 类关系图

```
                    ┌─────────────────────┐
                    │  CombatRoundManager │  回合状态机（~120 行）
                    │                     │
                    │ 事件:               │
                    │  RoundStarted ──────┼──→ RoundPhaseManager
                    │  UnitTurnStarted ───┼──→ UnitTurnHandler (玩家侧)
                    │                     │  └→ AITurnRunner (敌方侧)
                    │  UnitTurnEnded ─────┼──→ RoundPhaseManager
                    │  CombatEnded ───────┼──→ VictoryHandler / UI
                    └─────────────────────┘
                              │
                              │ EndTurn(unit)
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌─────────────────┐ ┌───────────────┐ ┌──────────────────┐
│ RoundPhaseManager│ │UnitTurnHandler│ │  AITurnRunner    │
│                 │ │               │ │                  │
│ 订阅:           │ │ 订阅:         │ │ 订阅:            │
│ RoundStarted    │ │ UnitTurnStart │ │ UnitTurnStart    │
│ UnitTurnEnded   │ │ (F=Player)    │ │ (F=Enemy)        │
│                 │ │               │ │                  │
│ 执行:           │ │ 玩家操作:     │ │ AI 决策:         │
│ sys_round_start │ │ 选中/切换/    │ │ 候选评估/执行    │
│ sys_turn_end    │ │ 输入路由      │ │                  │
└─────────────────┘ └───────────────┘ └──────────────────┘
          │                                          │
          │ 通过 SkillExecutor 执行技能               │
          ▼                                          ▼
┌─────────────────────────────────────────────────────────┐
│                   SkillExecutor                         │
│                                                         │
│  系统技能:                                              │
│  ├── sys_round_start  (RestoreAP + ResetMove +          │
│  │                      Cooldown + StatusTick)          │
│  └── sys_turn_end     (StatusTick + DecrementDuration)  │
│                                                         │
│  所有效果通过 Tag 条件 实现 debuff 阻断：                │
│  ├── State.APBlocked → RestoreAP 失效                   │
│  ├── State.Rooted   → ResetMovement 失效                │
│  └── State.Silenced → 任意 Tag 阻断                     │
└─────────────────────────────────────────────────────────┘
```

## 9. debuff 阻断的完整示例

### 场景：Boss 战，Boss 释放"虚弱诅咒"阻止玩家 AP 恢复

```
1. Boss 释放 crippling_hex → AddStatusEffect(hex_ap_block)
   hex_ap_block:
   ├── appliedTags: [State.APBlocked, State.Debuff]
   ├── durationRounds: 2
   ├── stackRule: RefreshDuration
   └── statModifiers: [Attack -3]

2. 回合开始，RoundPhaseManager 对玩家执行 sys_round_start:

   SkillExecutor.Execute(sys_round_start, player)
   ├── CanExecute:
   │   ├── Skill 非空 ✓
   │   ├── Caster 存活 ✓
   │   ├── AP=0, apCost=0 → 够 ✓
   │   └── EvaluateTagConditions:  (target=Self)
   │       ├── requiredCasterTags: [] → ✓
   │       ├── blockedCasterTags: [] → ✓
   │       ├── requiredTargetTags: [] → ✓
   │       └── blockedTargetTags: [State.APBlocked]
   │           → 玩家有 State.APBlocked → ❌ FAIL!
   │
   └── 返回: TagConditionFailed
       "Target is blocked by tag 'State.APBlocked'."

3. 结果:
   - AP 没有恢复 ← 被 Tag 阻断
   - 移动预算没有重置 ← 被 Tag 阻断 (如果配置了)
   - 冷却仍然推进 ← 不受影响
   - Status tick 仍然触发 ← 不受影响 (DOT 照样扣血)
```

### 配置实现（纯数据）

`sys_round_start` 技能的 `blockedTargetTags` 配置：
```
blockedTargetTags[0] = State.APBlocked      ← 有则跳过 RestoreAP
blockedTargetTags[1] = State.Rooted         ← 有则跳过 ResetMovement
```

如果想更细粒度的阻断（只阻断 AP 恢复但不阻断移动重置），可以：
- 方案A：拆成两个独立系统技能 `sys_restore_ap` + `sys_reset_movement`，各自配置独立的 `blockedTargetTags`
- 方案B：在每个 Effect 上增加 `BlockedTags` 字段（EffectDefinition 扩展）

**推荐方案A**，因为系统技能可以自由组合，`RoundPhaseManager` 遍历技能列表即可：

```csharp
[SerializeField] private SkillDefinition[] m_roundStartSkills; // 可配置多个

private void OnRoundStarted(int roundCount)
{
    foreach (var unit in aliveUnits)
    {
        foreach (var skill in m_roundStartSkills)
        {
            unit.GetComponent<SkillExecutor>().Execute(skill, unit);
            // 某个技能因 Tag 阻断失败不影响其他技能
        }
    }
}
```

这样 `sys_restore_ap` 和 `sys_reset_movement` 是两个独立技能，阻断互不影响。

## 10. 实施步骤

| 步骤 | 内容 | 依赖 |
|------|------|------|
| 1 | 创建 `CombatUnit` 标记组件 | — |
| 2 | 创建 `RestoreAttributeEffect` | — |
| 3 | 创建 `ResetMovementEffect` | — |
| 4 | 创建 `AdvanceCooldownsEffect` | — |
| 5 | 创建 `TriggerStatusTickEffect` | StatusComponent (CR-0013) |
| 6 | 创建 `DecrementStatusDurationEffect` | StatusComponent (CR-0013) |
| 7 | 通过 Unity Editor 创建 `sys_round_start` 和 `sys_turn_end` 技能资产 | 1-6 |
| 8 | 精简 `CombatRoundManager` 到 ~120 行 | 1 |
| 9 | 创建 `RoundPhaseManager` | 7, 8 |
| 10 | 创建 `UnitTurnHandler`（玩家侧） | 8 |
| 11 | 重写 `AITurnRunner`（敌方侧） | 8 |
| 12 | 废弃 `EnemySpawner`，场景预置敌方单位 | 1 |
| 13 | 清理 `Player1Controller` / `EnemyController` pass-through 属性 | 10, 11 |
| 14 | 创建 UnitRegistry | 1 |
| 15 | 验证：`crippling_hex` 可以阻止 AP 恢复 | 全部 |

## 11. 与现有 IS 问题对照

| IS 问题 | 是否解决 | 方式 |
|---------|---------|------|
| IS-0003 Manager 职责过重 | ✅ | 拆分为 4 个类 |
| IS-0004 DefaultInstance | ✅ | 技能资产显式配置 |
| IS-0005 新旧 API 桥接 | ✅ | 直接访问 AttributeSet |
| IS-0006 #if UNITY_EDITOR | ✅ | 不再加载技能资产 |
| IS-0007 Faction 同步重复 | ✅ | 通过 CombatUnit + AttributeSet |
| IS-0008 EnemySpawner 临时API | ✅ | 场景预置单位 |
| IS-0009 CombatTrigger | → | 见 Docs/13 §3 |
| IS-0011 冷却不支持探索 | → | CooldownTracker 扩展 |
| IS-0016 事件总线 | ✅ | 回合事件系统 |

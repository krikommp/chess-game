# 13 - 后续框架设计 (Future Framework Design)

> 2026-05-10 基于代码审查结果，对尚未实现的系统进行详细设计。设计目标：保持与现有代码的兼容性，遵循 Tag First 原则，数据驱动优先。

---

## 1. Status 系统设计

### 1.1 概述

Status 系统是技能系统的直接下游，负责管理单位身上的持续性状态（buff/debuff/持续伤害/持续恢复等）。Skill 通过 `AddStatusEffectDefinition` 添加 Status，Status 在特定时机触发自己的 Effect。

### 1.2 核心数据结构

#### StatusDefinition (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "MiniChess/Status Definition")]
public class StatusDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string m_id;
    [SerializeField] private string m_displayName;
    [SerializeField, TextArea(1,3)] private string m_description;

    [Header("Duration")]
    [Tooltip("持续回合数。0 = 立即触发一次后移除。负数 = 永久(需手动移除)。")]
    [SerializeField] private int m_durationRounds = 1;

    [Header("Stacking")]
    [Tooltip("同名 Status 叠加规则")]
    [SerializeField] private EStatusStackRule m_stackRule = EStatusStackRule.RefreshDuration;

    [Header("Tags")]
    [Tooltip("此 Status 会给目标添加的 Tag")]
    [SerializeField] private GameplayTag[] m_appliedTags;

    [Header("Tick Effects")]
    [Tooltip("回合开始时触发的效果")]
    [SerializeField] private EffectDefinition[] m_onTurnStartEffects;
    [Tooltip("回合结束时触发的效果")]
    [SerializeField] private EffectDefinition[] m_onTurnEndEffects;

    [Header("Stat Modifiers")]
    [Tooltip("持续期间对属性的修正")]
    [SerializeField] private StatModifier[] m_statModifiers;

    [Header("Visual")]
    [Tooltip("状态图标")]
    [SerializeField] private Sprite m_icon;
    [Tooltip("状态 VFX 预制体")]
    [SerializeField] private GameObject m_vfxPrefab;
}
```

**路径：** `Assets/Data/Statuses/`

#### EStatusStackRule

```csharp
public enum EStatusStackRule
{
    RefreshDuration,  // 刷新持续时间（默认）
    StackValue,       // 叠加数值层数
    IgnoreIfExists,   // 已存在则忽略新添加
    Replace           // 替换为新 Status
}
```

#### StatModifier (可序列化结构)

```csharp
[Serializable]
public struct StatModifier
{
    [Tooltip("要修正的属性 Tag")]
    [SerializeField] private GameplayTag m_attributeTag;

    [Tooltip("修正类型")]
    [SerializeField] private EModifierType m_modifierType;

    [Tooltip("修正值")]
    [SerializeField] private float m_value;

    public GameplayTag AttributeTag => m_attributeTag;
    public EModifierType ModifierType => m_modifierType;
    public float Value => m_value;
}

public enum EModifierType
{
    Add,             // 加法修正: current = base + value
    Multiply,        // 乘法修正: current = base * (1 + value)
    Override         // 覆盖: current = value
}
```

#### StatusInstance (运行时状态实例)

```csharp
/// <summary>
/// 单个 Status 的运行时实例。由 StatusComponent 管理。
/// </summary>
public sealed class StatusInstance
{
    public StatusDefinition Definition { get; }
    public int RemainingRounds { get; set; }
    public int StackCount { get; set; }
    public GameObject Source { get; }  // 谁施加了这个 Status

    public StatusInstance(StatusDefinition definition, GameObject source)
    {
        Definition = definition;
        Source = source;
        RemainingRounds = definition.DurationRounds;
        StackCount = 1;
    }
}
```

#### StatusComponent (MonoBehaviour)

```csharp
/// <summary>
/// 挂在 GameObject 上，管理该单位所有运行时 Status。
/// 与 GameplayTagComponent 和 AttributeSet 紧密配合。
/// </summary>
public class StatusComponent : MonoBehaviour
{
    private readonly List<StatusInstance> m_statuses = new List<StatusInstance>();
    private GameplayTagComponent m_tagComponent;
    private AttributeSet m_attributeSet;

    public IReadOnlyList<StatusInstance> ActiveStatuses => m_statuses;
    public event Action<StatusInstance> StatusAdded;
    public event Action<StatusInstance> StatusRemoved;

    void Awake()
    {
        m_tagComponent = GetComponent<GameplayTagComponent>();
        m_attributeSet = GetComponent<AttributeSet>();
    }

    // ── 添加 Status ──

    public StatusInstance AddStatus(StatusDefinition definition, GameObject source)
    {
        // 检查堆叠规则
        var existing = FindStatus(definition.Id);
        switch (definition.StackRule)
        {
            case EStatusStackRule.IgnoreIfExists:
                if (existing != null) return existing;
                break;
            case EStatusStackRule.RefreshDuration:
                if (existing != null)
                {
                    existing.RemainingRounds = definition.DurationRounds;
                    return existing;
                }
                break;
            case EStatusStackRule.Replace:
                if (existing != null)
                    RemoveStatus(existing);
                break;
            case EStatusStackRule.StackValue:
                if (existing != null)
                {
                    existing.StackCount++;
                    existing.RemainingRounds = definition.DurationRounds;
                    return existing;
                }
                break;
        }

        var instance = new StatusInstance(definition, source);
        m_statuses.Add(instance);

        // 添加 Tag
        foreach (var tag in definition.AppliedTags)
            m_tagComponent?.AddTag(tag, instance);  // source = StatusInstance

        // 应用属性修正
        ApplyStatModifiers(definition.StatModifiers, 1f);

        StatusAdded?.Invoke(instance);
        return instance;
    }

    // ── 移除 Status ──

    public void RemoveStatus(StatusInstance instance)
    {
        if (!m_statuses.Remove(instance)) return;

        // 移除该 Status 添加的所有 Tag（利用来源追踪）
        m_tagComponent?.RemoveAllTagsFromSource(instance);

        // 移除属性修正
        RemoveStatModifiers(instance.Definition.StatModifiers, 1f);

        StatusRemoved?.Invoke(instance);
    }

    public void RemoveStatusById(string statusId)
    {
        var instance = FindStatus(statusId);
        if (instance != null) RemoveStatus(instance);
    }

    // ── 回合推进 ──

    public void AdvanceTurn()
    {
        // 回合开始触发 tick
        for (int i = m_statuses.Count - 1; i >= 0; i--)
        {
            var s = m_statuses[i];
            ApplyTickEffects(s.Definition.OnTurnStartEffects, s.Source);

            s.RemainingRounds--;
            if (s.RemainingRounds == 0)
            {
                // 回合结束触发 tick（最后一轮）
                ApplyTickEffects(s.Definition.OnTurnEndEffects, s.Source);
                RemoveStatus(s);
            }
        }
    }

    public void AdvanceRealtime(float deltaSeconds)
    {
        // 探索模式下的实时冷却推进（后续扩展）
    }

    // ── 查询 ──

    public StatusInstance FindStatus(string statusId)
    {
        foreach (var s in m_statuses)
            if (s.Definition.Id == statusId) return s;
        return null;
    }

    public bool HasStatus(string statusId) => FindStatus(statusId) != null;

    public bool HasStatusWithTag(GameplayTag tag)
    {
        return m_tagComponent?.HasTag(tag) ?? false;
    }

    // ── 属性修正 ──

    private void ApplyStatModifiers(StatModifier[] modifiers, float multiplier)
    {
        if (modifiers == null || m_attributeSet == null) return;
        foreach (var mod in modifiers)
        {
            float delta = mod.Value * multiplier;
            switch (mod.ModifierType)
            {
                case EModifierType.Add:
                    m_attributeSet.Modify(mod.AttributeTag, delta);
                    break;
                case EModifierType.Multiply:
                    float current = m_attributeSet.Get(mod.AttributeTag);
                    m_attributeSet.Modify(mod.AttributeTag, current * delta);
                    break;
                // Override 需要独立的临时覆写系统，建议后续扩展
            }
        }
    }

    private void RemoveStatModifiers(StatModifier[] modifiers, float multiplier)
    {
        if (modifiers == null || m_attributeSet == null) return;
        foreach (var mod in modifiers)
        {
            float delta = mod.Value * multiplier;
            switch (mod.ModifierType)
            {
                case EModifierType.Add:
                    m_attributeSet.Modify(mod.AttributeTag, -delta);
                    break;
                case EModifierType.Multiply:
                    float current = m_attributeSet.Get(mod.AttributeTag);
                    m_attributeSet.Modify(mod.AttributeTag, -current * delta / (1 + delta));
                    break;
            }
        }
    }

    private void ApplyTickEffects(EffectDefinition[] effects, GameObject source)
    {
        if (effects == null) return;
        var executor = GetComponent<SkillExecutor>();
        foreach (var effect in effects)
        {
            if (effect == null) continue;
            var context = new EffectContext
            {
                Caster = source,
                Target = gameObject,
                CasterExecutor = source != null ? source.GetComponent<SkillExecutor>() : null,
                TargetExecutor = executor,
            };
            effect.Apply(context);
        }
    }
}
```

### 1.3 AddStatusEffectDefinition 完整实现

```csharp
public class AddStatusEffectDefinition : EffectDefinition
{
    [SerializeField] private StatusDefinition m_statusDefinition;
    [SerializeField] private int m_durationTurnsOverride = -1; // -1 = 使用 StatusDefinition 默认

    public override ETargetCapability RequiredCapability => ETargetCapability.Statusable;

    public override void Apply(EffectContext context)
    {
        if (context.Target == null || m_statusDefinition == null) return;

        var statusComp = context.Target.GetComponent<StatusComponent>();
        if (statusComp == null)
        {
            Debug.LogWarning($"[Effect] Target '{context.Target.name}' has no StatusComponent.");
            return;
        }

        var instance = statusComp.AddStatus(m_statusDefinition, context.Caster);
        if (m_durationTurnsOverride > 0)
            instance.RemainingRounds = m_durationTurnsOverride;
    }
}
```

### 1.4 Status 系统与现有系统的交互

```
                  SkillDefinition
                      │
                      ▼
               SkillExecutor.Execute
                      │
          ┌───────────┴───────────┐
          ▼                       ▼
    DamageEffect.Apply    AddStatusEffect.Apply
    HealEffect.Apply            │
          │                     ▼
          │            StatusComponent.AddStatus
          │                     │
          │         ┌───────────┼───────────┐
          │         ▼           ▼           ▼
          │    添加 Tag     应用属性修正   记录 StatusInstance
          │    (GameplayTag  (AttributeSet
          │     Component)   .Modify)
          │
          ▼
    AttributeSet.Modify("Attribute.HP", delta)
```

### 1.5 验证技能示例

| Status ID | 类型 | 持续 | 效果 |
|-----------|------|------|------|
| `buffed_attack` | Buff | 2 回合 | `StatModifier(Attribute.Attack, Add, 5)` |
| `guarding_defense` | Buff | 2 回合 | `StatModifier(Attribute.Defense, Add, 5)` |
| `weakened_attack` | Debuff | 2 回合 | `StatModifier(Attribute.Attack, Add, -3)` |
| `poison_dot` | Debuff | 3 回合 | `OnTurnStart: DamageEffect(5)` |
| `regen_hot` | Buff | 3 回合 | `OnTurnStart: HealEffect(3)` |

---

## 2. AI 行为系统设计

> **下一轮将进行完整的 AI 框架重设计。本节仅保留关键设计约束和方向性描述。**

### 2.1 设计约束

- **所有单位统一用 `GameObject`**。不允许 AI 框架依赖 `Player1Controller`、`EnemyController` 等具体类型。访问属性、技能、Tag 均通过 `GetComponent<AttributeSet>()` / `GetComponent<SkillExecutor>()` / `GetComponent<GameplayTagComponent>()`。
- **AIProfile 挂在 EnemyTurnRunner 的按单位配置上**，或挂在一个轻量 `AIUnitConfig` 组件上，不挂在已删除的 `EnemyController` 上。
- **AI 和玩家走同一技能入口**：`EnemyTurnRunner` 构造 `SkillExecutionContext` → 调用 `SkillExecutor.Execute(ctx)`。AI 不能绕过 SkillExecutor 直接操作属性或移动。
- **Tag First**：AI 的倾向性通过 AIProfile 的 Tag 权重表达，不写硬编码的 role 分支。
- **决策输出必须可解释**：每次 AI 行动输出结构化的候选列表、评分明细、最终选择和原因。

### 2.2 决策流程

```
候选生成 → 可用性过滤 → 评分 → 选择 → 执行 → Debug 输出

1. 候选生成：遍历单位可用技能 × 合法目标，每个 (技能, 目标) 生成一个候选
2. 可用性过滤：校验 AP、冷却、目标存活、范围、Tag 条件
3. 评分：基础权重(AIProfile) + Tag权重 + HP紧迫度 + 距离/AP效率
4. 选择：最高分候选。无可用候选时 fallback 靠近或结束回合
5. 执行：交给 SkillExecutor，先移动(如果需要)再释放技能
6. Debug：输出每个候选的可行性、评分明细、失败原因
```

### 2.3 关键数据结构（草案）

- **AIActionCandidate**：Skill + Target + MoveDestination + MoveApCost + SkillApCost + Score + FailureReason
- **AIScoreBreakdown**：BaseWeight + SkillTagScore + TargetTagScore + HpUrgencyScore + DistanceScore + ApEfficiencyScore
- **AIActionEvaluator**（static class）：`Evaluate(GameObject unit, IReadOnlyList<GameObject> allTargets)` → 返回最优候选
- **EnemyTurnRunner**（MonoBehaviour）：订阅 `UnitTurnStarted` → 调 `AIActionEvaluator.Evaluate` → 通过 `SkillExecutor` 执行最优候选

### 2.4 ScriptedTactic（后续扩展）

脚本化战术是关卡/怪物设计者在特定条件下覆盖 AI 决策的机制。Phase 1 不实现，仅保留扩展点：
- 触发条件：`RoundEquals`、`SelfHpBelowPercent`、`AnyAllyHpBelowPercent`、`EnemiesClustered`
- 响应行为：`ForceSkill`、`BoostSkillTag`、`BlockSkillTag`、`ForceTarget`、`PrioritizeTargetTag`
- 用 ScriptableObject 配置，不影响通用 AI 评估流程

---

## 3. 战斗触发与流程设计

### 3.1 战斗状态机

```
       ┌──────────────────────────────┐
       │        Exploration           │
       │  (自由移动, 实时冷却,        │
       │   无回合概念)                │
       └──────────┬───────────────────┘
                  │ CombatTrigger 触发
                  ▼
       ┌──────────────────────────────┐
       │      CombatInitialize        │
       │  (收集参战单位, 锁定先攻,    │
       │   冻结非参战单位,            │
       │   切换到回合模式)            │
       └──────────┬───────────────────┘
                  │
                  ▼
       ┌──────────────────────────────┐
       │      SortByInitiative        │
       └──────────┬───────────────────┘
                  │
                  ▼
       ┌──────────────────────────────┐
  ┌───▶│        UnitTurn             │
  │    │  (玩家可控块 或 AI行动)     │
  │    └──────────┬───────────────────┘
  │               │ 单位AP用完/主动结束
  │               ▼
  │    ┌──────────────────────────────┐
  │    │     NextUnit                 │
  │    │  (队首下一个存活单位)       │
  │    └──────────┬───────────────────┘
  │               │
  │    ┌──────────┴──────┐
  │    │ 还有未行动单位?   │
  │    └──────────┬──────┘
  │      No       │       Yes
  │               │        └──── 回到 UnitTurn
  │               ▼
  │    ┌──────────────────────────────┐
  │    │       RoundEnd               │
  │    │  (检查胜负条件)             │
  │    └──────────┬───────────────────┘
  │               │
  │    ┌──────────┴──────────┐
  │    │ 战斗继续?             │
  │    └──────────┬──────────┘
  │      Yes      │       No
  │      (下一轮)  │        │
  └──────────────┘        ▼
                  ┌──────────────────────────────┐
                  │       CombatEnd              │
                  │  (结算经验/掉落, 解冻单位,   │
                  │   切回探索模式)              │
                  └──────────────────────────────┘
```

### 3.2 CombatTrigger

```csharp
public class CombatTrigger : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("触发方式")]
    [SerializeField] private ETriggerMode m_triggerMode = ETriggerMode.OnProximity;

    [Tooltip("触发半径（近接触发时）")]
    [SerializeField] private float m_triggerRadius = 5f;

    [Tooltip("检测层级")]
    [SerializeField] private LayerMask m_detectLayer;  // Player 层

    [Header("Participants")]
    [Tooltip("本场战斗的敌方单位")]
    [SerializeField] private MonsterSpawnPoint[] m_enemyParticipants;

    [Header("Combat Config")]
    [Tooltip("可选的战斗配置覆写（胜利条件等）")]
    [SerializeField] private CombatConfig m_combatConfig;

    [Header("Debug")]
    [SerializeField] private bool m_showTriggerRadius = true;

    private bool m_isTriggered;

    public enum ETriggerMode
    {
        OnProximity,     // 玩家进入触发半径
        OnAttack,        // 玩家主动攻击
        Scripted,        // 剧情脚本触发
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_isTriggered) return;
        if (m_triggerMode != ETriggerMode.OnProximity) return;

        var unit = other.GetComponent<AttributeSet>();
        if (unit != null && unit.Faction == EFaction.Player)
        {
            StartCombat();
        }
    }

    public void StartCombat()
    {
        if (m_isTriggered) return;
        m_isTriggered = true;

        var combatManager = FindObjectOfType<CombatRoundManager>();
        if (combatManager != null)
        {
            combatManager.StartCombatWithConfig(m_enemyParticipants, m_combatConfig);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!m_showTriggerRadius) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, m_triggerRadius);
    }
}
```

### 3.3 VictoryConditionEvaluator

```csharp
public class VictoryConditionEvaluator : MonoBehaviour
{
    [SerializeField] private CombatRoundManager m_combatManager;

    public event Action<EBattleResult> BattleEnded;

    public enum EBattleResult { Victory, Defeat, Retreat }

    public EBattleResult? Evaluate()
    {
        // 收集所有参战单位
        bool anyPlayerAlive = false;
        bool anyEnemyAlive = false;

        var allUnits = FindObjectsOfType<AttributeSet>();
        foreach (var unit in allUnits)
        {
            if (!unit.IsAlive) continue;
            if (unit.Faction == EFaction.Player) anyPlayerAlive = true;
            if (unit.Faction == EFaction.Enemy) anyEnemyAlive = true;
        }

        if (!anyPlayerAlive) return EBattleResult.Defeat;
        if (!anyEnemyAlive) return EBattleResult.Victory;
        return null; // 战斗继续
    }
}
```

### 3.4 CombatConfig (全局战斗配置)

```csharp
[CreateAssetMenu(menuName = "MiniChess/Combat Config")]
public class CombatConfig : ScriptableObject
{
    [Header("Defaults")]
    [SerializeField] private int m_defaultMaxAP = 6;
    [SerializeField] private float m_defaultMoveSpeed = 2f;

    [Header("Victory")]
    [Tooltip("自定义胜利条件。空 = 敌方全灭。")]
    [SerializeField] private VictoryCondition[] m_victoryConditions;

    [Header("AI")]
    [SerializeField] private bool m_enableAIDebug = true;
}
```

---

## 4. 事件系统设计

### 4.1 概述

当前战斗事件（伤害、治疗、技能施放、死亡等）通过 `Debug.Log` 输出，没有统一的事件分发机制。后续 UI 更新、VFX 触发、音效、成就、教程都需要订阅这些事件。

### 4.2 设计原则

- **Tag First**：事件语义通过 GameplayTag 表达
- **弱类型数据**：事件附带强类型数据（伤害值、位置等）
- **轻量**：不引入完整的事件总线框架，使用 C# event + 静态分发

### 4.3 核心结构

```csharp
/// <summary>
/// 战斗事件数据。Tag 表达事件语义，字段表达事件数据。
/// </summary>
public struct CombatEvent
{
    public GameplayTag EventTag;          // 事件标签，如 Event.Unit.Damaged
    public GameplayTag[] ContextTags;     // 上下文标签，如 Effect.Damage.Physical
    public GameObject Source;             // 事件来源
    public GameObject Target;             // 事件目标
    public Vector3? Position;             // 事件位置
    public float Value;                   // 主要数值（伤害量、治疗量等）
    public SkillDefinition Skill;         // 关联技能
    public StatusDefinition Status;       // 关联状态
}

/// <summary>
/// 战斗事件分发器。单例，挂在场景 Systems/CombatEventBus 上。
/// </summary>
public class CombatEventBus : MonoBehaviour
{
    public static CombatEventBus Instance { get; private set; }

    public event Action<CombatEvent> OnCombatEvent;

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public static void Fire(CombatEvent evt)
    {
        Instance?.OnCombatEvent?.Invoke(evt);
    }
}
```

### 4.4 集成点

需要在以下位置发出事件：

| 触发点 | 事件 Tag | 额外数据 |
|--------|---------|---------|
| 技能施放开始 | `Event.Skill.Cast` | Skill, Source, Target |
| 技能命中 | `Event.Skill.Hit` | Skill, Source, Target, ContextTags |
| 伤害应用 | `Event.Unit.Damaged` | Source, Target, Value, ContextTags |
| 治疗应用 | `Event.Unit.Healed` | Source, Target, Value |
| Status 添加 | `Event.Status.Added` | Status, Source, Target |
| Status 移除 | `Event.Status.Removed` | Status, Target |
| 单位死亡 | `Event.Unit.Died` | Target, Source(击杀者) |
| 回合开始 | `Event.Turn.Started` | Target(当前单位) |
| 回合结束 | `Event.Turn.Ended` | Target |
| 轮次开始 | `Event.Round.Started` | Value(RoundIndex) |
| 战斗开始 | `Event.Combat.Started` | — |
| 战斗结束 | `Event.Combat.Ended` | Value(结果: 0=败 1=胜) |

### 4.5 AttributeSet 集成

```csharp
// AttributeSet.Modify 中增加事件发送:
public float Modify(GameplayTag tag, float delta)
{
    float current = Get(tag);
    float result = ClampToMax(tag, Mathf.Max(0f, current + delta));

    if (!Mathf.Approximately(current, result))
    {
        m_currentValues[tag] = result;
        AttributeChanged?.Invoke(tag, current, result);

        // 发送战斗事件
        if (tag == WellKnownAttributeTags.HP)
        {
            float actualDelta = result - current;
            var eventTag = actualDelta < 0
                ? new GameplayTag("Event.Unit.Damaged")
                : new GameplayTag("Event.Unit.Healed");
            CombatEventBus.Fire(new CombatEvent
            {
                EventTag = eventTag,
                Target = gameObject,
                Value = Mathf.Abs(actualDelta),
            });
        }

        if (result <= 0f && current > 0f)
        {
            CombatEventBus.Fire(new CombatEvent
            {
                EventTag = new GameplayTag("Event.Unit.Died"),
                Target = gameObject,
            });
            AttributeDepleted?.Invoke(tag);
        }
    }

    return result;
}
```

---

## 5. 配置校验系统设计

### 5.1 概述

在 Docs/08 §8.5 基础上设计完整的配置校验系统。校验分为**编辑时校验**（Editor 窗口）和**运行时校验**（战斗启动时）。

### 5.2 校验规则清单

#### SkillDefinition 校验

| 规则 | 级别 | 说明 |
|------|------|------|
| `id` 非空 | Error | 技能必须有唯一 ID |
| `id` 全项目唯一 | Error | 遍历所有 SkillDefinition 资产 |
| `apCost >= 0` | Warning | 允许 0 AP 技能 |
| `cooldown >= 0` | Error | 负冷却非法 |
| `range >= 0` | Error | 负范围非法 |
| `effects` 至少一个 | Warning | 提示可能遗漏 |
| 每个 Effect 引用非空 | Error | 空引用导致运行时 NullRef |
| 每个 Effect 有至少一个 Tag | Error | Tag First 原则 |
| `targetType == GroundPoint` 必须有 Ability | Error | 地面点技能需要移动能力 |
| `targetType == SingleEnemy/Ally` 且 Ability==null 且 effects 为空 | Error | 无行为且无效果的技能无意义 |

#### Effect 校验

| 规则 | 级别 | 说明 |
|------|------|------|
| 有至少一个 Tag | Error | 每个 Effect 必须带 Tag |
| Tag 可以在 TagRegistry 中找到 | Warning | 提示未注册 Tag |
| `RequiredCapability` 不是 None | Warning | 提示可能遗漏 |

#### Status 校验

| 规则 | 级别 | 说明 |
|------|------|------|
| `id` 非空 | Error | |
| `appliedTags` 至少一个 | Warning | Status 应该给目标添加 Tag |
| `durationRounds > 0` 或明确标记为永久 | Warning | |

#### 单位校验

| 规则 | 级别 | 说明 |
|------|------|------|
| 有 AttributeSet | Error | 所有单位必须有属性容器 |
| 有 SkillExecutor | Error | 所有单位必须有技能执行器 |
| SkillExecutor.availableSkills 包含 basic_move | Error | 所有可移动单位必须有移动技能 |
| 有 MovementController | Error | 所有可移动单位必须有移动控制器 |
| 有 GameplayTagComponent | Warning | 用于 Tag 查询 |

### 5.3 校验入口

```csharp
public static class CombatConfigValidator
{
    public struct ValidationResult
    {
        public string AssetPath;
        public string FieldName;
        public EValidationSeverity Severity;
        public string Message;
    }

    public enum EValidationSeverity { Error, Warning, Info }

    /// <summary>校验项目中所有战斗配置资产</summary>
    public static List<ValidationResult> ValidateAll()
    {
        var results = new List<ValidationResult>();

        // 收集所有相关资产
        var allSkills = LoadAllAssets<SkillDefinition>("Assets/Data/Skills");
        var allEffects = LoadAllAssets<EffectDefinition>("Assets/Data/Effects");
        var allStatuses = LoadAllAssets<StatusDefinition>("Assets/Data/Statuses");
        var allAIProfiles = LoadAllAssets<AIProfile>("Assets/Data/AI");

        // 执行各类型校验
        foreach (var skill in allSkills) ValidateSkill(skill, allSkills, results);
        foreach (var effect in allEffects) ValidateEffect(effect, results);
        foreach (var status in allStatuses) ValidateStatus(status, results);

        return results;
    }

    // ... 具体校验实现 ...
}
```

---

## 6. 数据层扩展建议

### 6.1 UnitRegistry（单位注册中心）

替代 `CombatRoundManager.CacheUnits()` 中的 `FindObjectsOfType` 重复调用：

```csharp
/// <summary>
/// 管理场景中所有参战单位的注册和发现。
/// 单位通过 AttributeSet 组件自动注册。
/// </summary>
public class UnitRegistry : MonoBehaviour
{
    public static UnitRegistry Instance { get; private set; }

    private readonly List<GameObject> m_allUnits = new List<GameObject>();
    private readonly List<GameObject> m_playerUnits = new List<GameObject>();
    private readonly List<GameObject> m_enemyUnits = new List<GameObject>();

    public IReadOnlyList<GameObject> AllUnits => m_allUnits;
    public IReadOnlyList<GameObject> PlayerUnits => m_playerUnits;
    public IReadOnlyList<GameObject> EnemyUnits => m_enemyUnits;

    public event Action<GameObject> UnitRegistered;
    public event Action<GameObject> UnitUnregistered;

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Register(GameObject unit)
    {
        var attr = unit.GetComponent<AttributeSet>();
        if (attr == null) return;

        m_allUnits.Add(unit);
        if (attr.Faction == EFaction.Player) m_playerUnits.Add(unit);
        else m_enemyUnits.Add(unit);

        UnitRegistered?.Invoke(unit);
    }

    public void Unregister(GameObject unit)
    {
        m_allUnits.Remove(unit);
        m_playerUnits.Remove(unit);
        m_enemyUnits.Remove(unit);
        UnitUnregistered?.Invoke(unit);
    }
}
```

`AttributeSet.Awake()` 中自动注册：
```csharp
private void Awake()
{
    if (m_definition != null) BuildFromDefinition();
    SyncFactionTag();
    UnitRegistry.Instance?.Register(gameObject);  // 新增
}
```

### 6.2 冷却系统扩展

```csharp
/// <summary>
/// 冷却追踪器，支持回合模式和实时模式。
/// </summary>
public class CooldownTracker
{
    private readonly Dictionary<string, float> m_cooldowns = new();

    /// <summary>记录冷却。在战斗模式下 rounds=true，探索模式 rounds=false。</summary>
    public void Record(string skillId, int cooldownRounds, bool isCombatMode)
    {
        m_cooldowns[skillId] = isCombatMode ? (float)cooldownRounds : cooldownRounds * 6f;
    }

    /// <summary>战斗回合推进</summary>
    public void AdvanceTurn()
    {
        var keys = new List<string>(m_cooldowns.Keys);
        foreach (var key in keys)
        {
            m_cooldowns[key]--;
            if (m_cooldowns[key] <= 0) m_cooldowns.Remove(key);
        }
    }

    /// <summary>实时模式推进（deltaTime 秒）</summary>
    public void AdvanceRealtime(float deltaTime)
    {
        var keys = new List<string>(m_cooldowns.Keys);
        foreach (var key in keys)
        {
            m_cooldowns[key] -= deltaTime;
            if (m_cooldowns[key] <= 0) m_cooldowns.Remove(key);
        }
    }

    public int GetRemainingRounds(string skillId)
    {
        m_cooldowns.TryGetValue(skillId, out float v);
        return v > 0 ? Mathf.CeilToInt(v) : 0;
    }

    public bool IsOnCooldown(string skillId) => GetRemainingRounds(skillId) > 0;

    public void Reset() => m_cooldowns.Clear();
}
```

---

## 7. 实施建议顺序

基于 Docs/08 §7 的推荐顺序，结合本次审查发现的问题：

### 第一阶段：基础设施加固（1-2 轮）

1. **IS-0004 修复** — 确保 `basic_move.asset` 显式挂载 `GroundMoveAbility`，移除 `DefaultInstance` fallback
2. **IS-0008 修复** — EnemySpawner 使用 `AttributeSetDef` 替代 `Testing_AddAttribute`
3. **IS-0006 修复** — 移除 `#if UNITY_EDITOR` 资产加载
4. **IS-0005 修复** — 清理 EnemyTurnRunner 新旧 API 桥接

### 第二阶段：核心系统补齐（2-3 轮）

5. **IS-0001 实现** — Status 系统完整实现（StatusDefinition + StatusComponent + AddStatusEffect）
6. **IS-0002 实现** — AI Action Candidate 系统（AIActionCandidate + AIActionEvaluator）
7. **EnemyTurnRunner 重构** — 使用 AIActionEvaluator
8. **第一批技能创建** — `minor_heal`、`guarding_shout`（验证 Status 和 AI）

### 第三阶段：品质提升（1-2 轮）

9. **IS-0016 实现** — 事件总线（CombatEventBus）
10. **IS-0010 实现** — 完整配置校验
11. **IS-0009 实现** — CombatTrigger + VictoryConditionEvaluator
12. **第二批技能创建** — `power_strike`、`crippling_hex`

### 第四阶段：重构优化（按需）

13. **IS-0003 改进** — CombatRoundManager 职责拆分
14. **IS-0012 优化** — TagRegistry 改用 Dictionary
15. **UnitRegistry** — 单位注册中心实现
16. **IS-0011 扩展** — 冷却系统支持探索模式

---

## 8. 新增 Open Questions 建议

以下问题应在 `OPEN_QUESTIONS.md` 中追踪：

- **Q-0029** Status 的属性修正（StatModifier）是否需要独立的修改值追踪，还是直接修改 AttributeSet？
- **Q-0030** AI 候选评估中，"距离分"和"AP 效率分"的具体公式和权重是否需要策划可配置？
- **Q-0031** 战斗事件总线是否需要在事件中附带完整的 AttributeSet 快照（用于 UI 动效等）？
- **Q-0032** ScriptedTactic 的配置方式：ScriptableObject 资产 vs 关卡数据内嵌 vs 可视化节点？
- **Q-0033** 同一 Status 多次添加时，AttributeSet 的修正值（如 Attack+5 叠加两次）是否需要追踪每层来源以便精确回退？

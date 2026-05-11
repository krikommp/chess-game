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

## 2. AI Action Candidate 系统设计

### 2.1 概述

将 `EnemyTurnRunner` 从硬编码"追最近玩家 + 攻击"重构为通用的 AI 候选评估系统。决策流程：候选生成 → 可用性过滤 → 评分 → 选择 → 执行 → Debug 输出。

### 2.2 核心数据结构

#### AIActionCandidate

```csharp
public struct AIActionCandidate
{
    /// <summary>要使用的技能</summary>
    public SkillDefinition Skill;

    /// <summary>技能目标</summary>
    public GameObject Target;

    /// <summary>移动目的地（如果不在技能范围内）</summary>
    public Vector3 MoveDestination;

    /// <summary>NavMesh 移动路径</summary>
    public NavMeshPath MovePath;

    /// <summary>移动部分预计 AP 消耗</summary>
    public int MoveApCost;

    /// <summary>技能 AP 消耗</summary>
    public int SkillApCost;

    /// <summary>总 AP 消耗</summary>
    public int TotalApCost => MoveApCost + SkillApCost;

    /// <summary>是否本回合可释放（AP + 路径有效）</summary>
    public bool CanExecuteThisTurn;

    /// <summary>是否已在范围内（无需移动）</summary>
    public bool IsAlreadyInRange;

    /// <summary>是否仅移动（无法释放技能，只靠近）</summary>
    public bool IsFallbackOnly;

    /// <summary>评分</summary>
    public float Score;

    /// <summary>评分明细（用于 Debug）</summary>
    public AIScoreBreakdown ScoreBreakdown;

    /// <summary>不可用原因（可用的为 None）</summary>
    public ESkillCastFailure FailureReason;

    /// <summary>失败消息</summary>
    public string FailureMessage;

    public bool IsValid => Skill != null;
    public bool IsActionable => IsValid && FailureReason == ESkillCastFailure.None;
}
```

#### AIScoreBreakdown

```csharp
public struct AIScoreBreakdown
{
    public float BaseWeight;        // Skill.aiBaseWeight
    public float SkillTagScore;     // AIProfile.SkillTagWeights 匹配
    public float TargetTagScore;    // AIProfile.TargetTagWeights 匹配
    public float StatusTagScore;    // AIProfile.StatusTagWeights 匹配
    public float HpUrgencyScore;    // 治疗阈值 / 击杀阈值
    public float DistanceScore;     // 距离与 AP 效率
    public float ApEfficiencyScore; // AP 性价比
    public float TacticalBonus;     // Scripted Tactic 加成
    public float RiskPenalty;       // 风险减分

    public float Total =>
        BaseWeight + SkillTagScore + TargetTagScore + StatusTagScore
        + HpUrgencyScore + DistanceScore + ApEfficiencyScore
        + TacticalBonus - RiskPenalty;
}
```

### 2.3 核心组件

#### AIActionEvaluator

```csharp
/// <summary>
/// 静态工具类，负责候选的生成、过滤、评分和选择。
/// 分离 AI 逻辑以便测试和复用（玩家 AI 自动战斗、模拟等可复用）。
/// </summary>
public static class AIActionEvaluator
{
    /// <summary>
    /// 为给定敌方单位生成完整 AI 决策。
    /// </summary>
    public static AIActionCandidate Evaluate(
        GameObject enemyObject,
        IReadOnlyList<GameObject> playerUnits,
        IReadOnlyList<GameObject> enemyUnits,
        IReadOnlyList<GameObject> allTargets)
    {
        var executor = enemyObject.GetComponent<SkillExecutor>();
        var attr = enemyObject.GetComponent<AttributeSet>();
        var profile = enemyObject.GetComponent<EnemyController>()?.AIProfile;
        var movement = enemyObject.GetComponent<MovementController>();

        if (executor == null || attr == null || !attr.IsAlive)
            return default;

        // Step 1: 候选生成
        var candidates = GenerateCandidates(executor, attr, movement, playerUnits, enemyUnits, allTargets);

        // Step 2: 可用性过滤
        FilterCandidates(candidates, attr, movement);

        // Step 3: 评分
        ScoreCandidates(candidates, executor, attr, profile);

        // Step 4: Debug 输出
        LogCandidates(enemyObject, candidates, profile);

        // Step 5: 选择最高分
        return SelectBest(candidates);
    }

    // ── Step 1: 候选生成 ──

    private static List<AIActionCandidate> GenerateCandidates(
        SkillExecutor executor,
        AttributeSet attr,
        MovementController movement,
        IReadOnlyList<GameObject> playerUnits,
        IReadOnlyList<GameObject> enemyUnits,
        IReadOnlyList<GameObject> allTargets)
    {
        var candidates = new List<AIActionCandidate>();
        var skills = executor.AvailableSkills;
        int currentAp = (int)attr.Get(WellKnownAttributeTags.AP);

        foreach (var skill in skills)
        {
            if (skill == null) continue;

            var targets = ResolveTargetsForSkill(skill, executor.gameObject,
                playerUnits, enemyUnits, allTargets);

            foreach (var target in targets)
            {
                var candidate = BuildCandidate(executor, attr, movement, skill, target, currentAp);
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    private static List<GameObject> ResolveTargetsForSkill(
        SkillDefinition skill,
        GameObject self,
        IReadOnlyList<GameObject> playerUnits,
        IReadOnlyList<GameObject> enemyUnits,
        IReadOnlyList<GameObject> allTargets)
    {
        var targets = new List<GameObject>();
        var selfFaction = self.GetComponent<AttributeSet>()?.Faction ?? EFaction.Player;

        switch (skill.TargetType)
        {
            case ESkillTargetType.Self:
                targets.Add(self);
                break;

            case ESkillTargetType.SingleEnemy:
                foreach (var go in allTargets)
                {
                    var goAttr = go.GetComponent<AttributeSet>();
                    if (goAttr == null || !goAttr.IsAlive) continue;
                    if (goAttr.Faction != selfFaction)
                        targets.Add(go);
                }
                break;

            case ESkillTargetType.SingleAlly:
                foreach (var go in allTargets)
                {
                    var goAttr = go.GetComponent<AttributeSet>();
                    if (goAttr == null || !goAttr.IsAlive) continue;
                    if (goAttr.Faction == selfFaction)
                        targets.Add(go);
                }
                break;

            case ESkillTargetType.GroundPoint:
            case ESkillTargetType.Area:
                // 第一阶段跳过或生成占位候选
                break;
        }

        return targets;
    }

    private static AIActionCandidate BuildCandidate(
        SkillExecutor executor,
        AttributeSet attr,
        MovementController movement,
        SkillDefinition skill,
        GameObject target,
        int currentAp)
    {
        var candidate = new AIActionCandidate
        {
            Skill = skill,
            Target = target,
            SkillApCost = skill.Ability != null
                ? skill.Ability.GetApCost(SkillExecutionContext.ForTarget(executor, skill, target))
                : skill.ApCost,
        };

        // 检查是否已在范围内
        if (CombatMovementResolver.IsInRange(
            executor.transform.position, target.transform.position, skill.Range))
        {
            candidate.IsAlreadyInRange = true;
            candidate.CanExecuteThisTurn = candidate.SkillApCost <= currentAp;
            return candidate;
        }

        // 计算移动定位
        float remainingMove = movement?.RemainingMoveDistance ?? 0f;
        float moveSpeed = attr.Get(WellKnownAttributeTags.MoveSpeed);

        var positioning = CombatMovementResolver.Resolve(
            executor.transform.position,
            target.transform.position,
            skill.Range,
            candidate.SkillApCost,
            currentAp,
            remainingMove,
            moveSpeed);

        if (positioning.CanReachRange)
        {
            candidate.CanExecuteThisTurn = true;
            candidate.MoveDestination = positioning.Destination;
            candidate.MovePath = positioning.MovePath;
            candidate.MoveApCost = positioning.MoveApCost;
        }
        else if (positioning.HasFallback)
        {
            candidate.IsFallbackOnly = true;
            candidate.MoveDestination = positioning.FallbackDestination;
            candidate.MovePath = positioning.FallbackPath;
            candidate.MoveApCost = positioning.FallbackMoveApCost;
            candidate.CanExecuteThisTurn = false;
        }

        return candidate;
    }

    // ── Step 2: 可用性过滤 ──

    private static void FilterCandidates(
        List<AIActionCandidate> candidates,
        AttributeSet attr,
        MovementController movement)
    {
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            var c = candidates[i];
            var result = CheckCandidateAvailability(c, attr, movement);
            if (!result.IsSuccess)
            {
                // 保留不可用候选的失败原因供 Debug，但不参与最终选择
                c.FailureReason = result.Failure;
                c.FailureMessage = result.FailureMessage;
            }
            candidates[i] = c;
        }
    }

    private static SkillCastResult CheckCandidateAvailability(
        AIActionCandidate candidate,
        AttributeSet attr,
        MovementController movement)
    {
        if (candidate.Skill == null)
            return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Skill is null.");

        if (candidate.Target == null)
            return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Target is null.");

        var targetAttr = candidate.Target.GetComponent<AttributeSet>();
        if (targetAttr != null && !targetAttr.IsAlive)
            return SkillCastResult.Fail(ESkillCastFailure.TargetDead, "Target is dead.");

        return SkillCastResult.Success();
    }

    // ── Step 3: 评分 ──

    private static void ScoreCandidates(
        List<AIActionCandidate> candidates,
        SkillExecutor executor,
        AttributeSet attr,
        AIProfile profile)
    {
        foreach (var c in candidates)
        {
            if (c.FailureReason != ESkillCastFailure.None) continue;

            var breakdown = new AIScoreBreakdown();

            // 基础权��� (来自技能配置)
            breakdown.BaseWeight = c.Skill.AiBaseWeight;

            // AIProfile 权重
            if (profile != null)
            {
                // 技能 Tag 权重
                var skillAiTags = c.Skill.AiTags;
                for (int i = 0; i < skillAiTags.Length; i++)
                    breakdown.SkillTagScore += profile.GetSkillTagWeight(skillAiTags[i]) - 1f;

                // 目标 Tag 权重
                var targetTagComp = c.Target?.GetComponent<GameplayTagComponent>();
                if (targetTagComp != null)
                {
                    foreach (var tag in targetTagComp.TagSet.Tags)
                        breakdown.TargetTagScore += profile.GetTargetTagWeight(tag) - 1f;
                }
            }

            // HP 紧迫度
            var targetAttr = c.Target?.GetComponent<AttributeSet>();
            if (targetAttr != null)
            {
                float hpRatio = targetAttr.Get(WellKnownAttributeTags.HP)
                    / Mathf.Max(1f, targetAttr.GetMax(WellKnownAttributeTags.HP));

                // 治疗紧迫度：目标 HP 越低，治疗加成越高
                if (HasAiTag(c.Skill, AISkillTag.k_Heal) && profile != null)
                {
                    if (hpRatio <= profile.HealHpThreshold)
                        breakdown.HpUrgencyScore += profile.HealUrgencyBonus
                            * (1f - hpRatio / profile.HealHpThreshold);
                }

                // 击杀紧迫度：目标 HP 越低，伤害技能加成越高
                if (HasAiTag(c.Skill, AISkillTag.k_Damage) && c.CanExecuteThisTurn)
                {
                    int skillDamage = EstimateDamage(c.Skill, c.Target);
                    if (skillDamage >= targetAttr.Get(WellKnownAttributeTags.HP))
                        breakdown.HpUrgencyScore += 3f; // 可击杀的奖励
                }
            }

            // 距离与 AP 效率
            if (c.CanExecuteThisTurn && !c.IsAlreadyInRange)
            {
                // 需要移动的候选略低分
                breakdown.DistanceScore = -0.5f * c.MoveApCost;
            }
            if (c.IsFallbackOnly)
            {
                // 只靠近不攻击的候选大幅降分
                breakdown.ApEfficiencyScore = -5f;
            }

            c.Score = breakdown.Total;
            c.ScoreBreakdown = breakdown;
        }
    }

    // ── Step 4: 选择 ──

    private static AIActionCandidate SelectBest(List<AIActionCandidate> candidates)
    {
        AIActionCandidate best = default;
        float bestScore = float.MinValue;

        foreach (var c in candidates)
        {
            if (c.FailureReason != ESkillCastFailure.None) continue;
            if (c.Score > bestScore)
            {
                bestScore = c.Score;
                best = c;
            }
        }

        // 如果没有任何可用候选，选择分数最高的不可用候选（用于 Debug 输出）
        if (!best.IsValid && candidates.Count > 0)
        {
            foreach (var c in candidates)
            {
                if (c.Score > bestScore)
                {
                    bestScore = c.Score;
                    best = c;
                }
            }
        }

        return best;
    }

    // ── Step 5: Debug 输出 ──

    private static void LogCandidates(
        GameObject enemy,
        List<AIActionCandidate> candidates,
        AIProfile profile)
    {
        var attr = enemy.GetComponent<AttributeSet>();
        string enemyName = attr != null ? attr.DisplayName : enemy.name;

        Debug.Log($"[AI] === {enemyName} Decision ===");
        Debug.Log($"[AI] Profile: {(profile != null ? profile.name : "none")}, "
            + $"Role: {(profile != null ? profile.Role.ToString() : "default")}");

        foreach (var c in candidates)
        {
            string status = c.IsActionable
                ? (c.CanExecuteThisTurn ? "ACTIONABLE" : "MOVE-ONLY")
                : "BLOCKED";

            Debug.Log($"[AI]   [{status}] {c.Skill.DisplayName} → {c.Target?.name} "
                + $"Score={c.Score:F1} "
                + $"(base={c.ScoreBreakdown.BaseWeight:F1} "
                + $"skillTag={c.ScoreBreakdown.SkillTagScore:F1} "
                + $"targetTag={c.ScoreBreakdown.TargetTagScore:F1} "
                + $"hp={c.ScoreBreakdown.HpUrgencyScore:F1} "
                + $"dist={c.ScoreBreakdown.DistanceScore:F1} "
                + $"apEff={c.ScoreBreakdown.ApEfficiencyScore:F1} "
                + $"tactical={c.ScoreBreakdown.TacticalBonus:F1} "
                + $"risk={c.ScoreBreakdown.RiskPenalty:F1})");

            if (!c.IsActionable)
                Debug.Log($"[AI]     BLOCKED: {c.FailureMessage}");
        }
    }

    // ── Helpers ──

    private static bool HasAiTag(SkillDefinition skill, GameplayTag tag)
    {
        var aiTags = skill.AiTags;
        for (int i = 0; i < aiTags.Length; i++)
            if (aiTags[i] == tag) return true;
        return false;
    }

    private static int EstimateDamage(SkillDefinition skill, GameObject target)
    {
        var effects = skill.Effects;
        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i] is DamageEffectDefinition dmg)
                return dmg.Amount;
        }
        return 0;
    }
}
```

### 2.4 EnemyTurnRunner 重构

```csharp
public class EnemyTurnRunner : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float m_turnStartDelay = 0.25f;
    [SerializeField] private float m_afterActionDelay = 0.25f;
    [SerializeField] private float m_movementTimeoutSeconds = 8f;

    [Header("Debug")]
    [SerializeField] private bool m_enableDebugLog = true;
    [SerializeField] private bool m_verboseCandidateLog = false;

    public IEnumerator RunTurn(
        EnemyController enemy,
        IReadOnlyList<Player1Controller> playerUnits,
        IReadOnlyList<EnemyController> enemyUnits)
    {
        var enemyAttr = enemy.GetComponent<AttributeSet>();
        if (enemy == null || enemyAttr == null || !enemyAttr.IsAlive) yield break;

        yield return new WaitForSeconds(m_turnStartDelay);

        // 收集所有可被技能系统识别的目标
        var playerGOs = playerUnits
            .Where(p => p != null)
            .Select(p => p.gameObject)
            .ToList();
        var enemyGOs = enemyUnits
            .Where(e => e != null && e != enemy)
            .Select(e => e.gameObject)
            .ToList();
        var allTargets = new List<GameObject>();
        allTargets.AddRange(playerGOs);
        allTargets.AddRange(enemyGOs);

        // Step 1-4: AI 评估
        var best = AIActionEvaluator.Evaluate(
            enemy.gameObject, playerGOs, enemyGOs, allTargets);

        // Step 5: 执行
        if (best.IsActionable && best.CanExecuteThisTurn)
        {
            yield return ExecuteCandidate(enemy, best);
        }
        else if (best.IsActionable && best.IsFallbackOnly)
        {
            yield return ExecuteMoveOnly(enemy, best);
        }
        else
        {
            Debug.Log($"[AI] {enemy.DisplayName}: no actionable candidate. "
                + $"Best blocked: {best.Skill?.DisplayName ?? "none"} → {best.FailureMessage}");
        }

        yield return new WaitForSeconds(m_afterActionDelay);
    }

    private IEnumerator ExecuteCandidate(EnemyController enemy, AIActionCandidate candidate)
    {
        var movement = enemy.GetComponent<MovementController>();

        // 如果需要移动
        if (!candidate.IsAlreadyInRange && candidate.MovePath != null)
        {
            // 软占位检查（可提取到 AIActionEvaluator 中做）
            movement.TryStartMove(candidate.MovePath);

            float waitStart = Time.time;
            while (movement.IsMoving)
            {
                if (Time.time - waitStart > m_movementTimeoutSeconds)
                {
                    movement.StopMovement();
                    break;
                }
                yield return null;
            }
        }

        // 执行技能
        var executor = enemy.GetComponent<SkillExecutor>();
        var result = executor.ExecuteAfterMove(candidate.Skill, candidate.Target);

        if (result.IsSuccess)
            Debug.Log($"[AI] {enemy.DisplayName}: {candidate.Skill.DisplayName} → {candidate.Target.name}");
        else
            Debug.Log($"[AI] {enemy.DisplayName}: FAILED {candidate.Skill.DisplayName} — {result.FailureMessage}");
    }

    private IEnumerator ExecuteMoveOnly(EnemyController enemy, AIActionCandidate candidate)
    {
        var movement = enemy.GetComponent<MovementController>();
        if (candidate.MovePath != null && movement != null)
        {
            movement.TryStartMove(candidate.MovePath);
            float waitStart = Time.time;
            while (movement.IsMoving)
            {
                if (Time.time - waitStart > m_movementTimeoutSeconds)
                {
                    movement.StopMovement();
                    break;
                }
                yield return null;
            }
        }
    }
}
```

### 2.5 ScriptedTactic 设计

```csharp
/// <summary>
/// 脚本化战术：关卡/怪物特殊决策覆盖。不替代通用 AI，只用于明确的设计意图。
/// </summary>
public class ScriptedTactic : ScriptableObject
{
    [Header("Trigger")]
    [SerializeField] private TacticCondition[] m_conditions;

    [Header("Response")]
    [SerializeField] private TacticResponse[] m_responses;

    /// <summary>检查当前战斗状态是否满足触发条件</summary>
    public bool ShouldTrigger(TacticContext context)
    {
        foreach (var cond in m_conditions)
        {
            if (!cond.Evaluate(context)) return false;
        }
        return true;
    }

    /// <summary>应用战术响应（修改 AI 评分/行为）</summary>
    public void Apply(TacticContext context, ref AIActionCandidate candidate)
    {
        foreach (var resp in m_responses)
            resp.Apply(context, ref candidate);
    }
}

public struct TacticContext
{
    public GameObject Self;
    public IReadOnlyList<GameObject> Allies;
    public IReadOnlyList<GameObject> Enemies;
    public int RoundIndex;
}

// 战术条件基类
public enum ETacticConditionType
{
    RoundEquals,           // RoundIndex == value
    SelfHpBelowPercent,    // SelfHP < threshold
    AnyAllyHpBelowPercent, // 任何友方 HP < threshold
    EnemiesClustered,      // N 个敌人在 R 半径内
}

// 战术响应
public enum ETacticResponseType
{
    ForceSkill,            // 强制使用指定技能
    BoostSkillTag,         // 临时提高某类技能权重
    BlockSkillTag,         // 临时禁用某类技能
    ForceTarget,           // 强制选择指定类型目标
    PrioritizeTargetTag,   // 优先选择带某 Tag 的目标
}
```

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

        var player = other.GetComponent<Player1Controller>();
        if (player != null)
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

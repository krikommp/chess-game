using System;
using System.Collections.Generic;
using MiniChess.Combat;
using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public class SkillExecutor : MonoBehaviour
    {
        [Header("Skills")]
        [SerializeField] private SkillDefinition[] m_availableSkills;

        [Header("Target Capabilities")]
        [Tooltip("What effect types this object can receive.")]
        [SerializeField] private ETargetCapability m_capabilities = ETargetCapability.Damageable;

        private AttributeSet m_attributes;
        private MovementController m_movement;
        private GameplayTagComponent m_tagComp;
        private readonly Dictionary<string, int> m_cooldowns = new Dictionary<string, int>();
        private readonly List<ActiveEffect> m_activeEffects = new List<ActiveEffect>();
        private readonly List<SkillDefinition> m_grantedSkills = new List<SkillDefinition>();
        private SkillDefinition m_activeSkill;

        public SkillDefinition[] AvailableSkills
        {
            get
            {
                if (m_grantedSkills.Count == 0)
                    return m_availableSkills ?? Array.Empty<SkillDefinition>();

                var all = new List<SkillDefinition>();
                if (m_availableSkills != null) all.AddRange(m_availableSkills);
                all.AddRange(m_grantedSkills);
                return all.ToArray();
            }
        }

        public IReadOnlyList<ActiveEffect> ActiveEffects => m_activeEffects;
        public ETargetCapability Capabilities => m_capabilities;
        public SkillDefinition ActiveSkill => m_activeSkill;
        public MovementController Movement => m_movement;

        public int GetCooldownRemaining(string skillId) =>
            m_cooldowns.TryGetValue(skillId, out int v) ? v : 0;

        public AttributeSet Attributes
        {
            get
            {
                if (m_attributes == null)
                    m_attributes = GetComponent<AttributeSet>();
                return m_attributes;
            }
        }

        private void Awake()
        {
            m_movement = GetComponent<MovementController>();
        }

        // ── Skill access ────────────────────────────────────────────

        public SkillCastResult CanCast(SkillDefinition skill, GameObject target)
        {
            return CanExecute(SkillExecutionContext.ForTarget(this, skill, target));
        }

        public SkillDefinition FindSkill(string id)
        {
            var skills = AvailableSkills;
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] != null && skills[i].Id == id)
                    return skills[i];
            }
            return null;
        }

        public bool ActivateSkill(SkillDefinition skill)
        {
            if (skill == null || !HasSkill(skill))
            {
                m_activeSkill = null;
                return false;
            }

            m_activeSkill = skill;
            return true;
        }

        public void ClearActiveSkill()
        {
            m_activeSkill = null;
        }

        public void SetSkills(SkillDefinition[] skills)
        {
            m_availableSkills = skills;
        }

        private bool HasSkill(SkillDefinition skill)
        {
            var skills = AvailableSkills;
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] == skill) return true;
                if (skills[i] != null && skill != null && skills[i].Id == skill.Id) return true;
            }
            return false;
        }

        // ── Validation ──────────────────────────────────────────────

        public SkillCastResult CanExecute(SkillExecutionContext context)
        {
            var skill = context.Skill;
            if (skill == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Skill is null.");

            if (skill.Ability == null)
                return SkillCastResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    $"Skill '{skill.Id}' has no Ability configured. All castable skills must have an explicit Ability.");

            var casterAttr = Attributes;
            if (casterAttr == null || !casterAttr.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.CasterDead, "Caster is dead or missing AttributeSet.");

            // SkillDefinition-level tag conditions
            var tagResult = EvaluateSkillTagConditions(skill, context.Target);
            if (!tagResult.IsSuccess)
                return tagResult;

            // GroundPoint skills skip target validation
            if (skill.TargetType == ESkillTargetType.GroundPoint)
                return SkillCastResult.Success();

            var resolved = ResolveTarget(skill, context.Target);
            if (resolved.Target == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Target is null.");

            if (skill.TargetType == ESkillTargetType.Self)
                return SkillCastResult.Success();

            // Target alive check
            var targetAttr = resolved.Target?.GetComponent<AttributeSet>();
            if (targetAttr != null && !targetAttr.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.TargetDead, "Target is dead.");

            if (resolved.TargetExecutor == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetCapabilityBlocked,
                    "Target has no SkillExecutor and cannot be affected by skills.");

            // Faction check
            EFaction casterFaction = casterAttr.Faction;
            EFaction targetFaction = targetAttr?.Faction ?? EFaction.Player;

            bool factionMismatch = false;
            switch (skill.TargetType)
            {
                case ESkillTargetType.SingleEnemy:
                    factionMismatch = targetFaction == casterFaction;
                    break;
                case ESkillTargetType.SingleAlly:
                    factionMismatch = targetFaction != casterFaction;
                    break;
            }

            if (factionMismatch)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid,
                    $"Target faction '{targetFaction}' is invalid for {skill.TargetType}.");

            // Range check
            float dist = Vector3.Distance(gameObject.transform.position, resolved.Target.transform.position);
            if (dist > skill.Range + 0.05f)
                return SkillCastResult.Fail(ESkillCastFailure.OutOfRange,
                    $"Target is out of range ({dist:F1}m > {skill.Range}m).");

            return SkillCastResult.Success();
        }

        // ── Execution ────────────────────────────────────────────────

        public SkillCastResult Execute(SkillDefinition skill, GameObject target)
        {
            return Execute(SkillExecutionContext.ForTarget(this, skill, target));
        }

        public SkillCastResult Execute(SkillExecutionContext context)
        {
            // Generic validation
            var canExecute = CanExecute(context);
            if (!canExecute.IsSuccess)
                return canExecute;

            var skill = context.Skill;
            if (skill?.Ability == null)
                return SkillCastResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "Skill has no Ability configured.");

            // Delegate to Ability.Execute — all AP/Effect/Cooldown logic is inside the Ability
            return skill.Ability.Execute(context);
        }

        public SkillCastResult ExecuteAfterMove(SkillDefinition skill, GameObject target)
        {
            if (skill == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Skill is null.");

            if (target == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Target lost during approach.");

            var targetAttr = target.GetComponent<AttributeSet>();
            if (targetAttr != null && !targetAttr.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.TargetDead, "Target died during approach.");

            if (!CombatMovementResolver.IsInRange(transform.position, target.transform.position, skill.Range))
                return SkillCastResult.Fail(ESkillCastFailure.OutOfRange,
                    "Target moved out of range during approach.");

            return Execute(skill, target);
        }

        // ── Cooldowns ───────────────────────────────────────────────

        public void AdvanceCooldowns()
        {
            var keys = new List<string>(m_cooldowns.Keys);
            foreach (var key in keys)
            {
                m_cooldowns[key]--;
                if (m_cooldowns[key] <= 0)
                    m_cooldowns.Remove(key);
            }
        }

        public void SetCooldown(string skillId, int rounds)
        {
            if (string.IsNullOrEmpty(skillId) || rounds <= 0) return;
            m_cooldowns[skillId] = rounds;
        }

        public void ResetCooldowns()
        {
            m_cooldowns.Clear();
        }

        /// <summary>
        /// Legacy input routing. For non-move skills only.
        /// Move skill input is handled directly by UnitTurnHandler.
        /// </summary>
        public SkillCastResult HandleInputLegacy(SkillInputRequest request, SkillDefinition activeSkill)
        {
            if (activeSkill == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "No active skill.");

            // Non-move skills: just log for now (attack skills not yet implemented)
            return SkillCastResult.Success();
        }

        // ── Effect management ───────────────────────────────────────

        public void ApplyEffect(EffectDefinition effect, GameObject source)
        {
            if (effect == null) return;

            var ctx = new EffectContext
            {
                Caster = source ?? gameObject,
                Target = gameObject,
                CasterExecutor = source != null ? source.GetComponent<SkillExecutor>() : null,
                TargetExecutor = this,
            };

            effect.Apply(ctx, EffectResult.Success());

            if (!effect.IsPersistent) return;

            // Stack rule
            if (effect.DurationRounds > 0)
            {
                var existing = FindActiveEffect(effect);
                switch (effect.StackRule)
                {
                    case EStackRule.RefreshDuration:
                        if (existing != null) { existing.RemainingRounds = effect.DurationRounds; return; }
                        break;
                    case EStackRule.ExtendDuration:
                        if (existing != null) { existing.RemainingRounds += effect.DurationRounds; return; }
                        break;
                }
            }

            var active = new ActiveEffect(effect, source, effect.DurationRounds);
            m_activeEffects.Add(active);

            // Apply granted tags
            var grantedTags = effect.GrantedTags;
            if (grantedTags.Length > 0)
            {
                var tc = TagComp;
                for (int i = 0; i < grantedTags.Length; i++)
                    if (!string.IsNullOrEmpty(grantedTags[i].Value))
                        tc.AddTag(grantedTags[i], $"Effect.{effect.name}");
            }

            // Apply remove tags
            var removeTags = effect.RemoveTags;
            if (removeTags.Length > 0)
            {
                var tc = TagComp;
                for (int i = 0; i < removeTags.Length; i++)
                    if (!string.IsNullOrEmpty(removeTags[i].Value))
                        tc.RemoveTag(removeTags[i], $"Effect.{effect.name}");
            }

            // Apply granted abilities
            var abilities = effect.GrantedAbilities;
            for (int i = 0; i < abilities.Length; i++)
                if (abilities[i] != null)
                    m_grantedSkills.Add(abilities[i]);

            // Apply stat modifiers
            var mods = effect.StatModifiers;
            if (mods.Length > 0)
            {
                var attr = Attributes;
                if (attr != null)
                {
                    for (int i = 0; i < mods.Length; i++)
                    {
                        float value = mods[i].Type == EModifierType.Additive
                            ? mods[i].Value
                            : attr.GetMax(mods[i].Attribute) * mods[i].Value;
                        attr.Modify(mods[i].Attribute, value);
                    }
                }
            }
        }

        internal void RemoveActiveEffect(ActiveEffect active)
        {
            var effect = active.Definition;
            if (effect == null) return;

            // Remove granted abilities
            var abilities = effect.GrantedAbilities;
            for (int i = 0; i < abilities.Length; i++)
                m_grantedSkills.Remove(abilities[i]);

            // Remove granted tags
            var grantedTags = effect.GrantedTags;
            if (grantedTags.Length > 0)
            {
                var tc = TagComp;
                for (int i = 0; i < grantedTags.Length; i++)
                    tc.RemoveTag(grantedTags[i], $"Effect.{effect.name}");
            }

            // Reverse stat modifiers
            var mods = effect.StatModifiers;
            if (mods.Length > 0)
            {
                var attr = Attributes;
                if (attr != null)
                {
                    for (int i = 0; i < mods.Length; i++)
                    {
                        float value = mods[i].Type == EModifierType.Additive
                            ? mods[i].Value
                            : attr.GetMax(mods[i].Attribute) * mods[i].Value;
                        attr.Modify(mods[i].Attribute, -value);
                    }
                }
            }

            // OnRemove callback via Apply (stub)
            var ctx = new EffectContext
            {
                Caster = active.Source,
                Target = gameObject,
                CasterExecutor = active.Source != null ? active.Source.GetComponent<SkillExecutor>() : null,
                TargetExecutor = this,
            };
            effect.Apply(ctx, EffectResult.Success());

            m_activeEffects.Remove(active);
        }

        /// <summary>Called by CombatRoundManager / RoundPhaseManager at the start of each round.</summary>
        public void OnRoundStart()
        {
            AdvanceCooldowns();

            // Tick and expire active effects
            for (int i = m_activeEffects.Count - 1; i >= 0; i--)
            {
                var active = m_activeEffects[i];

                if (active.Definition.TickPerRound)
                {
                    var tickCtx = new EffectContext
                    {
                        Caster = active.Source,
                        Target = gameObject,
                        CasterExecutor = active.Source != null ? active.Source.GetComponent<SkillExecutor>() : null,
                        TargetExecutor = this,
                    };
                    active.Definition.Apply(tickCtx, EffectResult.Success());
                }

                if (active.Definition.DurationRounds > 0)
                {
                    active.RemainingRounds--;
                    if (active.RemainingRounds <= 0)
                        RemoveActiveEffect(active);
                }
            }
        }

        // ── Internals ───────────────────────────────────────────────

        private readonly struct ResolvedTarget
        {
            public readonly GameObject Target;
            public readonly SkillExecutor TargetExecutor;

            public ResolvedTarget(GameObject target, SkillExecutor executor)
            {
                Target = target;
                TargetExecutor = executor;
            }
        }

        private ResolvedTarget ResolveTarget(SkillDefinition skill, GameObject requestedTarget)
        {
            if (skill.TargetType == ESkillTargetType.Self)
                return new ResolvedTarget(gameObject, this);

            if (skill.TargetType == ESkillTargetType.GroundPoint)
                return new ResolvedTarget(gameObject, this);

            var executor = requestedTarget != null ? requestedTarget.GetComponent<SkillExecutor>() : null;
            return new ResolvedTarget(requestedTarget, executor);
        }

        private SkillCastResult EvaluateSkillTagConditions(SkillDefinition skill, GameObject target)
        {
            var casterTags = CollectTagsFrom(gameObject);

            var requiredCaster = skill.RequiredCasterTags;
            for (int i = 0; i < requiredCaster.Length; i++)
            {
                if (string.IsNullOrEmpty(requiredCaster[i].Value)) continue;
                if (!casterTags.Has(requiredCaster[i], ETagMatchMode.Exact))
                    return SkillCastResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Caster lacks required tag '{requiredCaster[i].Value}'.");
            }

            var blockedCaster = skill.BlockedCasterTags;
            for (int i = 0; i < blockedCaster.Length; i++)
            {
                if (string.IsNullOrEmpty(blockedCaster[i].Value)) continue;
                if (casterTags.Has(blockedCaster[i], ETagMatchMode.Exact))
                    return SkillCastResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Caster is blocked by tag '{blockedCaster[i].Value}'.");
            }

            // Skill self-target or ground point: skip target tag checks
            if (skill.TargetType == ESkillTargetType.Self || skill.TargetType == ESkillTargetType.GroundPoint)
                return SkillCastResult.Success();

            if (target == null) return SkillCastResult.Success();

            var targetTags = CollectTagsFrom(target);

            var requiredTarget = skill.RequiredTargetTags;
            for (int i = 0; i < requiredTarget.Length; i++)
            {
                if (string.IsNullOrEmpty(requiredTarget[i].Value)) continue;
                if (!targetTags.Has(requiredTarget[i], ETagMatchMode.Exact))
                    return SkillCastResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Target lacks required tag '{requiredTarget[i].Value}'.");
            }

            var blockedTarget = skill.BlockedTargetTags;
            for (int i = 0; i < blockedTarget.Length; i++)
            {
                if (string.IsNullOrEmpty(blockedTarget[i].Value)) continue;
                if (targetTags.Has(blockedTarget[i], ETagMatchMode.Exact))
                    return SkillCastResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Target is blocked by tag '{blockedTarget[i].Value}'.");
            }

            return SkillCastResult.Success();
        }

        private static GameplayTagSet CollectTagsFrom(GameObject obj)
        {
            var tags = new GameplayTagSet();
            if (obj == null) return tags;

            var tagComp = obj.GetComponent<GameplayTagComponent>();
            if (tagComp != null)
            {
                foreach (var tag in tagComp.TagSet.Tags)
                    tags.Add(tag, "SkillExecutor.CollectTags");
            }

            // Faction tags are auto-synced by AttributeSet.Awake → SyncFactionTag to GameplayTagComponent.
            // No duplicate syncing here.

            return tags;
        }

        private ActiveEffect FindActiveEffect(EffectDefinition def)
        {
            for (int i = 0; i < m_activeEffects.Count; i++)
                if (m_activeEffects[i].Definition == def)
                    return m_activeEffects[i];
            return null;
        }

        private GameplayTagComponent TagComp
        {
            get
            {
                if (m_tagComp == null)
                    m_tagComp = GetComponent<GameplayTagComponent>();
                if (m_tagComp == null)
                    m_tagComp = gameObject.AddComponent<GameplayTagComponent>();
                return m_tagComp;
            }
        }
    }

    [System.Serializable]
    public class ActiveEffect
    {
        public EffectDefinition Definition;
        public GameObject Source;
        public int RemainingRounds;

        public ActiveEffect(EffectDefinition def, GameObject source, int remainingRounds)
        {
            Definition = def;
            Source = source;
            RemainingRounds = remainingRounds;
        }
    }
}

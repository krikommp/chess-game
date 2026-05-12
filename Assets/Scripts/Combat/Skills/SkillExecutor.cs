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
        [SerializeField] private SkillAbility[] m_availableSkills;

        private AttributeSet m_attributes;
        private MovementController m_movement;
        private GameplayTagComponent m_tagComp;
        private readonly Dictionary<string, int> m_cooldowns = new Dictionary<string, int>();
        private readonly List<ActiveEffect> m_activeEffects = new List<ActiveEffect>();
        private readonly List<SkillAbility> m_grantedSkills = new List<SkillAbility>();
        private SkillAbility m_activeSkill;

        public SkillAbility[] AvailableSkills
        {
            get
            {
                if (m_grantedSkills.Count == 0)
                    return m_availableSkills ?? Array.Empty<SkillAbility>();

                var all = new List<SkillAbility>();
                if (m_availableSkills != null) all.AddRange(m_availableSkills);
                all.AddRange(m_grantedSkills);
                return all.ToArray();
            }
        }

        public IReadOnlyList<ActiveEffect> ActiveEffects => m_activeEffects;
        public SkillAbility ActiveSkill => m_activeSkill;
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

        public SkillCastResult CanCast(SkillAbility skill, GameObject target)
        {
            return CanExecute(SkillExecutionContext.ForTarget(this, skill, target));
        }

        public SkillAbility FindSkill(string id)
        {
            var skills = AvailableSkills;
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] != null && skills[i].Id == id)
                    return skills[i];
            }
            return null;
        }

        public bool ActivateSkill(SkillAbility skill)
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

        public void SetSkills(SkillAbility[] skills)
        {
            m_availableSkills = skills;
        }

        private bool HasSkill(SkillAbility skill)
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

            var casterAttr = Attributes;
            if (casterAttr == null || !casterAttr.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.CasterDead, "Caster is dead or missing AttributeSet.");

            var tagResult = EvaluateSkillTagConditions(skill, context.Target);
            if (!tagResult.IsSuccess)
                return tagResult;

            if (context.Target == null)
                return SkillCastResult.Success();

            var targetAttr = context.Target.GetComponent<AttributeSet>();
            if (targetAttr != null && !targetAttr.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.TargetDead, "Target is dead.");

            if (context.Target.GetComponent<SkillExecutor>() == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetCapabilityBlocked,
                    "Target has no SkillExecutor and cannot be affected by skills.");

            return SkillCastResult.Success();
        }

        // ── Execution ────────────────────────────────────────────────

        public SkillCastResult Execute(SkillAbility skill, GameObject target)
        {
            return Execute(SkillExecutionContext.ForTarget(this, skill, target));
        }

        public SkillCastResult Execute(SkillExecutionContext context)
        {
            var canExecute = CanExecute(context);
            if (!canExecute.IsSuccess)
                return canExecute;

            var skill = context.Skill;
            if (skill == null)
                return SkillCastResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "Skill is null.");

            return skill.Execute(context);
        }

        public SkillCastResult ExecuteAfterMove(SkillAbility skill, GameObject target)
        {
            if (skill == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Skill is null.");

            if (target == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Target lost during approach.");

            var targetAttr = target.GetComponent<AttributeSet>();
            if (targetAttr != null && !targetAttr.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.TargetDead, "Target died during approach.");

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

        public SkillCastResult HandleInputLegacy(SkillInputRequest request, SkillAbility activeSkill)
        {
            if (activeSkill == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "No active skill.");

            return SkillCastResult.Success();
        }

        // ── Effect management ───────────────────────────────────────

        public void ApplyEffect(SkillEffect effect, GameObject source)
        {
            if (effect == null) return;

            var ctx = new SkillEffectContext
            {
                Caster = source ?? gameObject,
                Target = gameObject,
                CasterExecutor = source != null ? source.GetComponent<SkillExecutor>() : null,
                TargetExecutor = this,
            };

            effect.Apply(ctx, SkillEffectResult.Success());

            if (!effect.IsPersistent) return;

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

            var ctx = new SkillEffectContext
            {
                Caster = active.Source,
                Target = gameObject,
                CasterExecutor = active.Source != null ? active.Source.GetComponent<SkillExecutor>() : null,
                TargetExecutor = this,
            };
            effect.Apply(ctx, SkillEffectResult.Success());

            m_activeEffects.Remove(active);
        }

        public void OnRoundStart()
        {
            AdvanceCooldowns();

            for (int i = m_activeEffects.Count - 1; i >= 0; i--)
            {
                var active = m_activeEffects[i];

                if (active.Definition.TickPerRound)
                {
                    var tickCtx = new SkillEffectContext
                    {
                        Caster = active.Source,
                        Target = gameObject,
                        CasterExecutor = active.Source != null ? active.Source.GetComponent<SkillExecutor>() : null,
                        TargetExecutor = this,
                    };
                    active.Definition.Apply(tickCtx, SkillEffectResult.Success());
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

        private SkillCastResult EvaluateSkillTagConditions(SkillAbility skill, GameObject target)
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

            return tags;
        }

        private ActiveEffect FindActiveEffect(SkillEffect def)
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
        public SkillEffect Definition;
        public GameObject Source;
        public int RemainingRounds;

        public ActiveEffect(SkillEffect def, GameObject source, int remainingRounds)
        {
            Definition = def;
            Source = source;
            RemainingRounds = remainingRounds;
        }
    }
}

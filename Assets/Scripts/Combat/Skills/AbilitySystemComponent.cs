using System;
using System.Collections.Generic;
using MiniChess.Combat;
using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [Serializable]
    public struct PassiveAbilityTrigger
    {
        [Tooltip("Skill definition to execute when the trigger tag is added.")]
        public SkillDefinition Definition;
        [Tooltip("Tag that activates this passive ability.")]
        public GameplayTag TriggerTag;
    }

    public class AbilitySystemComponent : MonoBehaviour
    {
        [Header("Skills")]
        [SerializeField] private SkillDefinition[] m_availableSkills;

        [Header("Passive Abilities")]
        [Tooltip("Skills that trigger automatically when specific tags are added.")]
        [SerializeField] private PassiveAbilityTrigger[] m_passiveAbilities;

        private AttributeSet m_attributes;
        private MovementController m_movement;
        private GameplayTagComponent m_tagComp;
        private readonly List<ActiveSkillEffect> m_activeEffects = new List<ActiveSkillEffect>();
        private readonly List<AbilitySpec> m_availableSpecs = new List<AbilitySpec>();
        private readonly List<AbilitySpec> m_grantedSpecs = new List<AbilitySpec>();
        private AbilitySpec m_activeSpec;
        private bool m_specsDirty = true;

        public IReadOnlyList<AbilitySpec> AvailableAbilities
        {
            get
            {
                RebuildAvailableSpecsIfNeeded();
                if (m_grantedSpecs.Count == 0)
                    return m_availableSpecs;

                var all = new List<AbilitySpec>(m_availableSpecs.Count + m_grantedSpecs.Count);
                all.AddRange(m_availableSpecs);
                all.AddRange(m_grantedSpecs);
                return all;
            }
        }

        public IReadOnlyList<ActiveSkillEffect> ActiveEffects => m_activeEffects;
        public AbilitySpec ActiveAbility => m_activeSpec;
        public MovementController Movement => m_movement;

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

        private void Start()
        {
            var tc = TagComp;
            if (tc != null)
                tc.OnTagAdded += OnTagAddedForPassive;
        }

        private void OnDestroy()
        {
            if (m_tagComp != null)
                m_tagComp.OnTagAdded -= OnTagAddedForPassive;
        }

        private void OnValidate()
        {
            m_specsDirty = true;
        }

        private void OnTagAddedForPassive(GameplayTag tag)
        {
            if (m_passiveAbilities == null) return;
            for (int i = 0; i < m_passiveAbilities.Length; i++)
            {
                var entry = m_passiveAbilities[i];
                if (entry.Definition == null) continue;
                if (entry.TriggerTag == tag)
                {
                    var spec = AbilitySpec.FromDefinition(entry.Definition, gameObject, this);
                    Execute(SkillExecutionContext.ForTarget(this, spec, gameObject));
                }
            }
        }

        public AbilitySpec FindAbility(string id)
        {
            var abilities = AvailableAbilities;
            for (int i = 0; i < abilities.Count; i++)
            {
                if (abilities[i] != null && abilities[i].Id == id)
                    return abilities[i];
            }
            return null;
        }

        public bool ActivateAbility(AbilitySpec spec)
        {
            if (spec == null || !HasAbility(spec))
            {
                m_activeSpec = null;
                return false;
            }

            m_activeSpec = spec;
            return true;
        }

        public void ClearActiveAbility()
        {
            m_activeSpec = null;
        }

        public void SetSkillDefinitions(SkillDefinition[] definitions)
        {
            m_availableSkills = definitions;
            m_specsDirty = true;
        }

        public SkillCastResult CanExecute(AbilitySpec spec, GameObject target)
        {
            return CanExecute(SkillExecutionContext.ForTarget(this, spec, target));
        }

        public SkillCastResult CanExecute(SkillExecutionContext context)
        {
            var spec = context.Spec;
            if (spec == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "AbilitySpec is null.");

            var definition = spec.Definition;
            if (definition == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "SkillDefinition is null.");

            var skill = spec.Ability;
            if (skill == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "SkillDefinition has no Ability.");

            var casterAttr = Attributes;
            if (casterAttr == null || !casterAttr.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.CasterDead, "Caster is dead or missing AttributeSet.");

            var tagResult = EvaluateSkillTagConditions(definition, context.Target);
            if (!tagResult.IsSuccess)
                return tagResult;

            if (context.Target == null)
                return SkillCastResult.Success();

            var targetAttr = context.Target.GetComponent<AttributeSet>();
            if (targetAttr != null && !targetAttr.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.TargetDead, "Target is dead.");

            if (context.Target.GetComponent<AbilitySystemComponent>() == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetCapabilityBlocked,
                    "Target has no AbilitySystemComponent and cannot be affected by skills.");

            return SkillCastResult.Success();
        }

        public SkillCastResult Execute(AbilitySpec spec, GameObject target)
        {
            return Execute(SkillExecutionContext.ForTarget(this, spec, target));
        }

        public SkillCastResult Execute(SkillExecutionContext context)
        {
            var canExecute = CanExecute(context);
            if (!canExecute.IsSuccess)
                return canExecute;

            var skill = context.Skill;
            if (skill == null)
                return SkillCastResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "SkillDefinition has no Ability.");

            return skill.Execute(context);
        }

        public SkillCastResult HandleInput(SkillInputRequest request)
        {
            if (m_activeSpec == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "No active ability.");

            var skill = m_activeSpec.Ability;
            if (!(skill is ISkillInputHandler inputHandler))
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid,
                    $"Active ability '{m_activeSpec.Id}' does not handle input.");

            var context = SkillExecutionContext.ForInput(this, m_activeSpec, request);
            return inputHandler.HandleInput(context);
        }

        public SkillEffectResult ApplyEffect(SkillEffect effect, GameObject source)
        {
            if (effect == null)
                return SkillEffectResult.Fail(ESkillCastFailure.EffectApplicationFailed, "Effect is null.");

            var ctx = new SkillEffectContext
            {
                Caster = source ?? gameObject,
                Target = gameObject,
                Source = source,
                CasterExecutor = source != null ? source.GetComponent<AbilitySystemComponent>() : null,
                TargetExecutor = this,
            };

            var computed = effect.Compute(ctx);
            if (!computed.IsSuccess)
                return computed;

            return ApplyEffect(effect, ctx, computed);
        }

        public SkillEffectResult ApplyEffect(
            SkillEffect effect,
            SkillEffectContext context,
            SkillEffectResult computed)
        {
            if (effect == null)
                return SkillEffectResult.Fail(ESkillCastFailure.EffectApplicationFailed, "Effect is null.");

            if (!computed.IsSuccess)
                return computed;

            var applied = effect.Apply(context, computed);
            if (!applied.IsSuccess)
                return applied;

            ApplyRemoveTags(effect);

            if (!effect.IsPersistent)
                return applied;

            var active = new ActiveSkillEffect(effect, context.Source ?? context.Caster, this, effect.DurationRounds);
            m_activeEffects.Add(active);

            ApplyGrantedTags(effect, active);
            ApplyGrantedAbilities(effect, active);
            ApplyStatModifiers(effect, active);

            return applied;
        }

        internal void RemoveActiveEffect(ActiveSkillEffect active)
        {
            var effect = active.Definition;
            if (effect == null) return;

            for (int i = m_grantedSpecs.Count - 1; i >= 0; i--)
            {
                if (m_grantedSpecs[i] != null && ReferenceEquals(m_grantedSpecs[i].GrantSource, active))
                    m_grantedSpecs.RemoveAt(i);
            }

            var grantedTags = effect.GrantedTags;
            if (grantedTags.Length > 0)
            {
                var tc = TagComp;
                for (int i = 0; i < grantedTags.Length; i++)
                    tc.RemoveTag(grantedTags[i], active);
            }

            var appliedModifiers = active.AppliedModifiers;
            if (appliedModifiers.Count > 0)
            {
                var attr = Attributes;
                if (attr != null)
                {
                    for (int i = 0; i < appliedModifiers.Count; i++)
                    {
                        var modifier = appliedModifiers[i];
                        attr.Modify(modifier.Attribute, -modifier.Value);
                    }
                }
            }

            m_activeEffects.Remove(active);
        }

        public void OnRoundStart()
        {
            for (int i = m_activeEffects.Count - 1; i >= 0; i--)
            {
                var active = m_activeEffects[i];

                if (active.Definition.TickPerRound)
                {
                    var tickCtx = new SkillEffectContext
                    {
                        Caster = active.Source,
                        Target = gameObject,
                        Source = active.Source,
                        CasterExecutor = active.Source != null ? active.Source.GetComponent<AbilitySystemComponent>() : null,
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

        private bool HasAbility(AbilitySpec spec)
        {
            if (spec == null) return false;

            var abilities = AvailableAbilities;
            for (int i = 0; i < abilities.Count; i++)
            {
                var candidate = abilities[i];
                if (candidate == spec) return true;
                if (candidate != null && candidate.Id == spec.Id) return true;
            }
            return false;
        }

        private void RebuildAvailableSpecsIfNeeded()
        {
            if (!m_specsDirty)
                return;

            m_availableSpecs.Clear();
            if (m_availableSkills != null)
            {
                for (int i = 0; i < m_availableSkills.Length; i++)
                {
                    if (m_availableSkills[i] != null)
                        m_availableSpecs.Add(AbilitySpec.FromDefinition(m_availableSkills[i], gameObject, this));
                }
            }

            m_specsDirty = false;
        }

        private void ApplyRemoveTags(SkillEffect effect)
        {
            var removeTags = effect.RemoveTags;
            if (removeTags.Length == 0) return;

            var tc = TagComp;
            for (int i = 0; i < removeTags.Length; i++)
                if (!string.IsNullOrEmpty(removeTags[i].Value))
                    tc.RemoveTag(removeTags[i]);
        }

        private void ApplyGrantedTags(SkillEffect effect, ActiveSkillEffect active)
        {
            var grantedTags = effect.GrantedTags;
            if (grantedTags.Length == 0) return;

            var tc = TagComp;
            for (int i = 0; i < grantedTags.Length; i++)
                if (!string.IsNullOrEmpty(grantedTags[i].Value))
                    tc.AddTag(grantedTags[i], active);
        }

        private void ApplyGrantedAbilities(SkillEffect effect, ActiveSkillEffect active)
        {
            var definitions = effect.GrantedAbilities;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] == null) continue;
                m_grantedSpecs.Add(AbilitySpec.FromDefinition(definitions[i], gameObject, active));
            }
        }

        private void ApplyStatModifiers(SkillEffect effect, ActiveSkillEffect active)
        {
            var mods = effect.StatModifiers;
            if (mods.Length == 0) return;

            var attr = Attributes;
            if (attr == null) return;

            for (int i = 0; i < mods.Length; i++)
            {
                float value = mods[i].Type == EModifierType.Additive
                    ? mods[i].Value
                    : attr.GetMax(mods[i].Attribute) * mods[i].Value;
                attr.Modify(mods[i].Attribute, value);
                active.RecordAppliedModifier(mods[i].Attribute, value);
            }
        }

        private SkillCastResult EvaluateSkillTagConditions(SkillDefinition definition, GameObject target)
        {
            var casterTags = CollectTagsFrom(gameObject);

            var requiredCaster = definition.RequiredCasterTags;
            for (int i = 0; i < requiredCaster.Length; i++)
            {
                if (string.IsNullOrEmpty(requiredCaster[i].Value)) continue;
                if (!casterTags.Has(requiredCaster[i], ETagMatchMode.Exact))
                    return SkillCastResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Caster lacks required tag '{requiredCaster[i].Value}'.");
            }

            var blockedCaster = definition.BlockedCasterTags;
            for (int i = 0; i < blockedCaster.Length; i++)
            {
                if (string.IsNullOrEmpty(blockedCaster[i].Value)) continue;
                if (casterTags.Has(blockedCaster[i], ETagMatchMode.Exact))
                    return SkillCastResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Caster is blocked by tag '{blockedCaster[i].Value}'.");
            }

            if (target == null) return SkillCastResult.Success();

            var targetTags = CollectTagsFrom(target);

            var requiredTarget = definition.RequiredTargetTags;
            for (int i = 0; i < requiredTarget.Length; i++)
            {
                if (string.IsNullOrEmpty(requiredTarget[i].Value)) continue;
                if (!targetTags.Has(requiredTarget[i], ETagMatchMode.Exact))
                    return SkillCastResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Target lacks required tag '{requiredTarget[i].Value}'.");
            }

            var blockedTarget = definition.BlockedTargetTags;
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
                    tags.Add(tag, "AbilitySystemComponent.CollectTags");
            }

            return tags;
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

    [Serializable]
    public class ActiveSkillEffect
    {
        public SkillEffect Definition;
        public GameObject Source;
        public AbilitySystemComponent Owner;
        public int RemainingRounds;
        public int StackCount = 1;
        private readonly List<AppliedStatModifier> m_appliedModifiers = new List<AppliedStatModifier>();

        public IReadOnlyList<AppliedStatModifier> AppliedModifiers => m_appliedModifiers;

        public ActiveSkillEffect(SkillEffect def, GameObject source, AbilitySystemComponent owner, int remainingRounds)
        {
            Definition = def;
            Source = source;
            Owner = owner;
            RemainingRounds = remainingRounds;
        }

        public void RecordAppliedModifier(GameplayTag attribute, float value)
        {
            m_appliedModifiers.Add(new AppliedStatModifier(attribute, value));
        }
    }

    public readonly struct AppliedStatModifier
    {
        public readonly GameplayTag Attribute;
        public readonly float Value;

        public AppliedStatModifier(GameplayTag attribute, float value)
        {
            Attribute = attribute;
            Value = value;
        }
    }
}

using System;
using System.Collections.Generic;
using MiniChess.Combat;
using MiniChess.GameplayTags;
using UnityEngine;
using UnityEngine.AI;

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
        private readonly Dictionary<string, int> m_cooldowns = new Dictionary<string, int>();
        private SkillDefinition m_activeSkill;

        public SkillDefinition[] AvailableSkills => m_availableSkills ?? Array.Empty<SkillDefinition>();
        public ETargetCapability Capabilities => m_capabilities;
        public SkillDefinition ActiveSkill => m_activeSkill;
        public int GetCooldownRemaining(string skillId) => m_cooldowns.TryGetValue(skillId, out int v) ? v : 0;

        public AttributeSet Attributes
        {
            get
            {
                if (m_attributes == null)
                    m_attributes = GetComponent<AttributeSet>();
                return m_attributes;
            }
        }

        public MovementController Movement
        {
            get
            {
                if (m_movement == null)
                    m_movement = GetComponent<MovementController>();
                return m_movement;
            }
        }

        public SkillCastResult CanCast(SkillDefinition skill, GameObject target)
        {
            return CanExecute(SkillExecutionContext.ForTarget(this, skill, target));
        }

        public SkillCastResult CanExecute(SkillExecutionContext context)
        {
            var skill = context.Skill;
            if (skill == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Skill is null.");

            var casterAttr = Attributes;
            if (casterAttr == null || !casterAttr.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.CasterDead, "Caster is dead or missing AttributeSet.");

            int apCost = GetApCost(context);
            if (casterAttr.Get(WellKnownAttributeTags.AP) < apCost)
                return SkillCastResult.Fail(ESkillCastFailure.InsufficientAp,
                    $"Need {apCost} AP, have {casterAttr.Get(WellKnownAttributeTags.AP)}.");

            if (m_cooldowns.TryGetValue(skill.Id, out int remaining) && remaining > 0)
                return SkillCastResult.Fail(ESkillCastFailure.OnCooldown,
                    $"'{skill.Id}' on cooldown ({remaining} turn(s) remaining).");

            var resolved = ResolveTarget(skill, context.Target);
            if (resolved.Target == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Target is null.");

            bool isSelf = skill.TargetType == ESkillTargetType.Self;
            bool isGroundPoint = skill.TargetType == ESkillTargetType.GroundPoint;

            if (!isSelf && !isGroundPoint)
            {
                var targetAttr = resolved.Target != null ? resolved.Target.GetComponent<AttributeSet>() : null;
                if (targetAttr != null && !targetAttr.IsAlive)
                    return SkillCastResult.Fail(ESkillCastFailure.TargetDead, "Target is dead.");

                if (resolved.TargetExecutor == null)
                    return SkillCastResult.Fail(ESkillCastFailure.TargetCapabilityBlocked,
                        "Target has no SkillExecutor and cannot be affected by skills.");
            }

            if (!isGroundPoint)
            {
                var effects = skill.Effects;
                for (int i = 0; i < effects.Length; i++)
                {
                    if (effects[i] == null) continue;
                    if ((resolved.TargetExecutor.Capabilities & effects[i].RequiredCapability) == 0)
                        return SkillCastResult.Fail(ESkillCastFailure.TargetCapabilityBlocked,
                            $"Target lacks capability '{effects[i].RequiredCapability}' for effect '{effects[i].name}'.");
                }
            }

            if (!isSelf && !isGroundPoint)
            {
                var targetAttr = resolved.Target != null ? resolved.Target.GetComponent<AttributeSet>() : null;
                EFaction targetFaction = targetAttr != null ? targetAttr.Faction : EFaction.Player;
                EFaction casterFaction = casterAttr.Faction;

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
            }

            if (!isSelf && !isGroundPoint)
            {
                float dist = Vector3.Distance(gameObject.transform.position, resolved.Target.transform.position);
                if (dist > skill.Range + 0.05f)
                    return SkillCastResult.Fail(ESkillCastFailure.OutOfRange,
                        $"Target is out of range ({dist:F1}m > {skill.Range}m).");
            }

            var tagResult = EvaluateTagConditions(skill, resolved.Target, skipTargetChecks: isGroundPoint);
            if (!tagResult.IsSuccess)
                return tagResult;

            return skill.Ability != null ? skill.Ability.CanApply(context) : SkillCastResult.Success();
        }

        public SkillCastResult Execute(SkillDefinition skill, GameObject target)
        {
            return Execute(SkillExecutionContext.ForTarget(this, skill, target));
        }

        public SkillCastResult Execute(SkillExecutionContext context)
        {
            var canExecute = CanExecute(context);
            if (!canExecute.IsSuccess)
                return canExecute;

            var skill = context.Skill;
            var casterAttr = Attributes;
            if (casterAttr == null)
                return SkillCastResult.Fail(ESkillCastFailure.CasterDead, "Caster has no AttributeSet.");

            var resolved = ResolveTarget(skill, context.Target);

            if (skill.Ability != null)
            {
                var abilityResult = skill.Ability.Apply(context);
                if (!abilityResult.IsSuccess)
                    return abilityResult;
            }

            int apCost = GetApCost(context);
            if (!casterAttr.TrySpend(WellKnownAttributeTags.AP, apCost))
                return SkillCastResult.Fail(ESkillCastFailure.InsufficientAp,
                    $"Failed to spend {apCost} AP after successful Apply.");

            var effectContext = new EffectContext
            {
                Caster = gameObject,
                Target = resolved.Target,
                CasterExecutor = this,
                TargetExecutor = resolved.TargetExecutor,
                TargetPosition = context.TargetPosition,
            };

            var effects = skill.Effects;
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] == null) continue;
                effects[i].Apply(effectContext);
            }

            if (skill.Cooldown > 0)
            {
                m_cooldowns[skill.Id] = skill.Cooldown;
            }

            return SkillCastResult.Success();
        }

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

        public void ResetCooldowns()
        {
            m_cooldowns.Clear();
        }

        public void SetSkills(SkillDefinition[] skills)
        {
            m_availableSkills = skills;
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

        public SkillCastResult HandleInput(SkillInputRequest inputRequest)
        {
            if (m_activeSkill == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "No active skill.");

            if (m_activeSkill.Ability == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid,
                    $"Active skill '{m_activeSkill.Id}' has no ability to handle input.");

            var context = SkillExecutionContext.ForInput(this, m_activeSkill, inputRequest);
            return m_activeSkill.Ability.HandleInput(context, inputRequest);
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

        private bool HasSkill(SkillDefinition skill)
        {
            var skills = AvailableSkills;
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] == skill)
                    return true;

                if (skills[i] != null && skill != null && skills[i].Id == skill.Id)
                    return true;
            }

            return false;
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

        private SkillCastResult EvaluateTagConditions(SkillDefinition skill, GameObject resolvedTarget, bool skipTargetChecks = false)
        {
            var casterTags = new GameplayTagSet();
            CollectTags(gameObject, casterTags);

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

            if (skipTargetChecks)
                return SkillCastResult.Success();

            var targetTags = new GameplayTagSet();
            CollectTags(resolvedTarget, targetTags);

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

        private static void CollectTags(GameObject obj, GameplayTagSet outTags)
        {
            if (obj == null) return;

            var tagComp = obj.GetComponent<GameplayTagComponent>();
            if (tagComp != null)
            {
                foreach (var tag in tagComp.TagSet.Tags)
                    outTags.Add(tag, "SkillExecutor.CollectTags");
            }

            // AttributeSet auto-syncs faction to GameplayTagComponent on Awake
            var attr = obj.GetComponent<AttributeSet>();
            if (attr != null)
            {
                var factionTag = new GameplayTag(attr.Faction == EFaction.Player ? "Faction.Player" : "Faction.Enemy");
                outTags.Add(factionTag, "SkillExecutor.FactionAutoSync");
            }
        }

        private static int GetApCost(SkillExecutionContext context)
        {
            if (context.Skill == null) return 0;
            return context.Skill.Ability != null
                ? context.Skill.Ability.GetApCost(context)
                : context.Skill.ApCost;
        }
    }
}

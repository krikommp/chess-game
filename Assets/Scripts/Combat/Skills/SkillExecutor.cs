using System.Collections.Generic;
using MiniChess.Combat;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public class SkillExecutor : MonoBehaviour
    {
        [Header("Skills")]
        public SkillDefinition[] availableSkills;

        [Header("Target Capabilities")]
        [Tooltip("What effect types this object can receive.")]
        [SerializeField] private ETargetCapability m_capabilities = ETargetCapability.Damageable;

        private ICombatUnit m_combatUnit;
        private readonly Dictionary<string, int> m_cooldowns = new Dictionary<string, int>();

        public SkillDefinition[] AvailableSkills => availableSkills ?? System.Array.Empty<SkillDefinition>();
        public ETargetCapability Capabilities => m_capabilities;
        public int GetCooldownRemaining(string skillId) => m_cooldowns.TryGetValue(skillId, out int v) ? v : 0;

        private ICombatUnit CombatUnit
        {
            get
            {
                if (m_combatUnit == null)
                    m_combatUnit = GetComponent<ICombatUnit>();
                return m_combatUnit;
            }
        }

        public SkillCastResult CanCast(SkillDefinition skill, GameObject target)
        {
            if (skill == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Skill is null.");

            var caster = CombatUnit;
            if (caster == null || !caster.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.CasterDead, "Caster is dead or missing ICombatUnit.");

            if (target == null && skill.TargetType != ESkillTargetType.Self)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Target is null.");

            if (caster.CurrentAP < skill.ApCost)
                return SkillCastResult.Fail(ESkillCastFailure.InsufficientAp,
                    $"Need {skill.ApCost} AP, have {caster.CurrentAP}.");

            if (m_cooldowns.TryGetValue(skill.Id, out int remaining) && remaining > 0)
                return SkillCastResult.Fail(ESkillCastFailure.OnCooldown,
                    $"'{skill.Id}' on cooldown ({remaining} turn(s) remaining).");

            if (skill.TargetType == ESkillTargetType.Self)
                return SkillCastResult.Success();

            if (target == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Target is null.");

            var targetUnit = target.GetComponent<ICombatUnit>();
            if (targetUnit != null && !targetUnit.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.TargetDead, "Target is dead.");

            // Check each effect's required capability against target
            var targetExecutor = target.GetComponent<SkillExecutor>();
            if (targetExecutor != null)
            {
                var effects = skill.Effects;
                for (int i = 0; i < effects.Length; i++)
                {
                    if (effects[i] == null) continue;
                    if ((targetExecutor.Capabilities & effects[i].RequiredCapability) == 0)
                        return SkillCastResult.Fail(ESkillCastFailure.TargetCapabilityBlocked,
                            $"Target lacks capability '{effects[i].RequiredCapability}' for effect '{effects[i].name}'.");
                }
            }

            return SkillCastResult.Success();
        }

        public SkillCastResult Execute(SkillDefinition skill, GameObject target)
        {
            var canCast = CanCast(skill, target);
            if (!canCast.IsSuccess)
                return canCast;

            var caster = CombatUnit;
            if (caster == null)
                return SkillCastResult.Fail(ESkillCastFailure.CasterDead, "Caster is null.");

            // Deduct AP
            caster.TrySpendAP(skill.ApCost);

            // Apply effects
            var targetExecutor = target != null ? target.GetComponent<SkillExecutor>() : null;
            var context = new EffectContext
            {
                Caster = gameObject,
                Target = target,
                CasterExecutor = this,
                TargetExecutor = targetExecutor,
            };

            var effects = skill.Effects;
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] == null) continue;
                effects[i].Apply(context);
            }

            // Record cooldown
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
            availableSkills = skills;
        }
    }
}

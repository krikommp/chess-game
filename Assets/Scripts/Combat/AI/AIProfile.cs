using System;
using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.AI
{
    [Serializable]
    public struct TagWeightEntry
    {
        [Tooltip("Tag to match against.")]
        [SerializeField] private GameplayTag m_tag;

        [Tooltip("Score multiplier when this tag matches.")]
        [SerializeField] private float m_weight;

        public GameplayTag Tag => m_tag;
        public float Weight => m_weight;
    }

    [CreateAssetMenu(fileName = "AIProfile", menuName = "MiniChess/AI Profile", order = 20)]
    public class AIProfile : ScriptableObject
    {
        [Header("Role")]
        [Tooltip("Default archetype tendency. Used as a baseline, not a hardcoded branch.")]
        [SerializeField] private EAIRole m_role = EAIRole.Aggressive;

        [Header("Skill Tag Weights")]
        [Tooltip("Bonus multiplier when a skill has one of these aiTags.")]
        [SerializeField] private TagWeightEntry[] m_skillTagWeights;

        [Header("Target Tag Weights")]
        [Tooltip("Bonus multiplier when a target unit carries one of these GameplayTags.")]
        [SerializeField] private TagWeightEntry[] m_targetTagWeights;

        [Header("Status Tag Weights")]
        [Tooltip("Bonus multiplier when a target or ally carries one of these status GameplayTags.")]
        [SerializeField] private TagWeightEntry[] m_statusTagWeights;

        [Header("Healing")]
        [Tooltip("HP ratio below which healing skills get a bonus (0.0–1.0).")]
        [SerializeField, Range(0f, 1f)] private float m_healHpThreshold = 0.5f;

        [Tooltip("Additional weight multiplier for healing skills when ally is below threshold.")]
        [SerializeField, Min(0f)] private float m_healUrgencyBonus = 2.0f;

        // ── Public properties ──────────────────────────────────────

        public EAIRole Role => m_role;
        public float HealHpThreshold => m_healHpThreshold;
        public float HealUrgencyBonus => m_healUrgencyBonus;

        public TagWeightEntry[] SkillTagWeights =>
            m_skillTagWeights ?? Array.Empty<TagWeightEntry>();
        public TagWeightEntry[] TargetTagWeights =>
            m_targetTagWeights ?? Array.Empty<TagWeightEntry>();
        public TagWeightEntry[] StatusTagWeights =>
            m_statusTagWeights ?? Array.Empty<TagWeightEntry>();

        // ── Weight lookups ─────────────────────────────────────────

        public float GetSkillTagWeight(GameplayTag tag)
        {
            if (string.IsNullOrEmpty(tag.Value)) return 1f;
            var entries = SkillTagWeights;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Tag == tag)
                    return entries[i].Weight;
            }
            return 1f;
        }

        public float GetTargetTagWeight(GameplayTag tag)
        {
            if (string.IsNullOrEmpty(tag.Value)) return 1f;
            var entries = TargetTagWeights;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Tag == tag)
                    return entries[i].Weight;
            }
            return 1f;
        }

        public float GetStatusTagWeight(GameplayTag tag)
        {
            if (string.IsNullOrEmpty(tag.Value)) return 1f;
            var entries = StatusTagWeights;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Tag == tag)
                    return entries[i].Weight;
            }
            return 1f;
        }

        /// <summary>
        /// Check whether a target HP ratio is below the heal threshold.
        /// </summary>
        public bool IsBelowHealThreshold(float hpRatio)
        {
            return hpRatio <= m_healHpThreshold;
        }
    }
}

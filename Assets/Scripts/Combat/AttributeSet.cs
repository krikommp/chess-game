using System;
using System.Collections.Generic;
using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Runtime attribute container. Owns current values keyed by GameplayTag, initialized from an
    /// AttributeSetDef ScriptableObject. Skills read/write attributes through this component rather
    /// than depending on any character type — any GameObject with AttributeSet is a valid skill target.
    /// </summary>
    public class AttributeSet : MonoBehaviour
    {
        [Header("Definition")]
        [Tooltip("Template asset defining attribute tags and base/max values. If null, values must be set programmatically.")]
        [SerializeField] private AttributeSetDef m_definition;

        [Header("Identity")]
        [SerializeField] private string m_displayName;
        [SerializeField] private EFaction m_faction = EFaction.Player;

        // ── Runtime state ──────────────────────────────────────────

        private readonly Dictionary<GameplayTag, float> m_currentValues = new Dictionary<GameplayTag, float>();
        private readonly Dictionary<GameplayTag, float> m_maxValues = new Dictionary<GameplayTag, float>();

        /// <summary>Tag → previous value, Tag → new value.</summary>
        public event Action<GameplayTag, float, float> AttributeChanged;

        /// <summary>Fired when a tag's current value reaches 0 from a positive value.</summary>
        public event Action<GameplayTag> AttributeDepleted;

        // ── Properties ──────────────────────────────────────────────

        public string DisplayName
        {
            get => string.IsNullOrWhiteSpace(m_displayName) ? gameObject.name : m_displayName;
            set => m_displayName = value;
        }

        public EFaction Faction => m_faction;

        /// <summary>Convenience: returns true when Attribute.HP > 0.</summary>
        public bool IsAlive => Get(WellKnownAttributeTags.HP) > 0f;

        // ── Unity ───────────────────────────────────────────────────

        private void Awake()
        {
            if (m_definition != null)
                BuildFromDefinition();

            // Auto-sync faction to GameplayTagComponent for Tag-based queries
            SyncFactionTag();
        }

        // ── Initialization ──────────────────────────────────────────

        public void BuildFromDefinition()
        {
            if (m_definition == null) return;

            m_currentValues.Clear();
            m_maxValues.Clear();

            var entries = m_definition.Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!entries[i].Tag.TryGetTag(out var tag)) continue;
                m_currentValues[tag] = entries[i].BaseValue;
                m_maxValues[tag] = entries[i].MaxValue;
            }
        }

        /// <summary>Override faction for test setups that don't use a definition asset.</summary>
        public void OverrideFactionForTesting(EFaction faction)
        {
            m_faction = faction;
            SyncFactionTag();
        }

        // ── Value access ────────────────────────────────────────────

        public float Get(GameplayTag tag)
        {
            m_currentValues.TryGetValue(tag, out float v);
            return v;
        }

        public float GetMax(GameplayTag tag)
        {
            m_maxValues.TryGetValue(tag, out float v);
            return v;
        }

        public void Set(GameplayTag tag, float value)
        {
            float clamped = ClampToMax(tag, Mathf.Max(0f, value));
            float prev = Get(tag);
            m_currentValues[tag] = clamped;

            if (!Mathf.Approximately(prev, clamped))
            {
                AttributeChanged?.Invoke(tag, prev, clamped);
                if (clamped <= 0f && prev > 0f)
                    AttributeDepleted?.Invoke(tag);
            }
        }

        public void SetMax(GameplayTag tag, float max)
        {
            m_maxValues[tag] = Mathf.Max(0f, max);
            // Re-clamp current value to new max
            float current = Get(tag);
            if (current > max)
                Set(tag, max);
        }

        /// <summary>Set current to MaxValue for the given tag. No-op if tag has no max defined.</summary>
        public void SetToMax(GameplayTag tag)
        {
            float max = GetMax(tag);
            if (max > 0f || m_maxValues.ContainsKey(tag))
                Set(tag, max);
        }

        /// <summary>Add delta to the current value. Clamped to [0, MaxValue]. Returns the new value.</summary>
        public float Modify(GameplayTag tag, float delta)
        {
            float current = Get(tag);
            float result = ClampToMax(tag, Mathf.Max(0f, current + delta));

            if (!Mathf.Approximately(current, result))
            {
                m_currentValues[tag] = result;
                AttributeChanged?.Invoke(tag, current, result);
                if (result <= 0f && current > 0f)
                    AttributeDepleted?.Invoke(tag);
            }

            return result;
        }

        /// <summary>Spend amount if current >= amount. Returns true on success.</summary>
        public bool TrySpend(GameplayTag tag, float amount)
        {
            if (amount <= 0f) return false;
            if (Get(tag) < amount) return false;
            Set(tag, Get(tag) - amount);
            return true;
        }

        // ── Helpers ─────────────────────────────────────────────────

        private float ClampToMax(GameplayTag tag, float value)
        {
            float max = GetMax(tag);
            return max > 0f ? Mathf.Min(value, max) : value;
        }

        private void SyncFactionTag()
        {
            var tagComp = GetComponent<GameplayTagComponent>();
            if (tagComp == null) return;

            var tag = new GameplayTag(m_faction == EFaction.Player ? "Faction.Player" : "Faction.Enemy");
            if (!tagComp.HasTag(tag, ETagMatchMode.Exact))
                tagComp.AddTag(tag, "AttributeSet.FactionAutoSync");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Programmatic attribute setup for tests. Only available in Editor / dev builds.</summary>
        public void Testing_AddAttribute(GameplayTag tag, float baseValue, float maxValue)
        {
            m_currentValues[tag] = baseValue;
            m_maxValues[tag] = maxValue;
        }
#endif
    }

    /// <summary>Well-known attribute tag constants to avoid string literals.</summary>
    public static class WellKnownAttributeTags
    {
        public static readonly GameplayTags.GameplayTag HP = "Attribute.HP";
        public static readonly GameplayTags.GameplayTag AP = "Attribute.AP";
        public static readonly GameplayTags.GameplayTag Initiative = "Attribute.Initiative";
        public static readonly GameplayTags.GameplayTag MoveSpeed = "Attribute.MoveSpeed";
    }
}

using System;
using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// ScriptableObject template defining which attributes a unit has and their base/max values.
    /// Referenced by AttributeSet at runtime for initialization. Does NOT store runtime state.
    /// </summary>
    [CreateAssetMenu(fileName = "AttributeSetDef", menuName = "MiniChess/Attribute Set Definition", order = 25)]
    public class AttributeSetDef : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Tag identifying this attribute, e.g. Attribute.HP, Attribute.AP, Attribute.MoveSpeed.")]
            public GameplayTag Tag;

            [Tooltip("Starting value when the unit is initialized or respawned.")]
            public float BaseValue;

            [Tooltip("Maximum value. Clamping only applies when MaxValue > 0. Set to 0 for no upper bound.")]
            public float MaxValue;
        }

        [SerializeField] private Entry[] m_entries;

        public Entry[] Entries => m_entries ?? Array.Empty<Entry>();
    }
}

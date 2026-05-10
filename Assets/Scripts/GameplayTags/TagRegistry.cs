using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.GameplayTags
{
    /// <summary>
    /// Global tag database for validation, documentation, and editor tooling.
    /// In the first phase this is a lightweight registry;
    /// the full editor window comes later (see Docs/08 §1.5).
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayTagRegistry", menuName = "MiniChess/GameplayTag Registry", order = 0)]
    public class TagRegistry : ScriptableObject
    {
        [SerializeField] private List<TagEntry> m_entries = new List<TagEntry>();

        public IReadOnlyList<TagEntry> Entries => m_entries;

        public bool TryGetEntry(GameplayTag tag, out TagEntry entry)
        {
            foreach (var e in m_entries)
            {
                if (e.Tag == tag)
                {
                    entry = e;
                    return true;
                }
            }
            entry = default;
            return false;
        }

        public bool IsRegistered(GameplayTag tag)
        {
            foreach (var e in m_entries)
                if (e.Tag == tag) return true;
            return false;
        }
    }

    [Serializable]
    public struct TagEntry
    {
        [SerializeField] private GameplayTagRef m_tag;
        [SerializeField] private string m_displayName;
        [SerializeField, TextArea(1, 3)]
        private string m_description;

        public GameplayTag Tag => m_tag;
        public string DisplayName => m_displayName;
        public string Description => m_description;

        public TagEntry(GameplayTag tag, string displayName, string description)
        {
            m_tag = tag.Value;
            m_displayName = displayName;
            m_description = description;
        }
    }
}


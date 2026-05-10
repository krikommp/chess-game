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
        [SerializeField] private List<TagEntry> _entries = new List<TagEntry>();

        public IReadOnlyList<TagEntry> Entries => _entries;

        public bool TryGetEntry(GameplayTag tag, out TagEntry entry)
        {
            foreach (var e in _entries)
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
            foreach (var e in _entries)
                if (e.Tag == tag) return true;
            return false;
        }
    }

    [Serializable]
    public struct TagEntry
    {
        [SerializeField] private GameplayTagRef _tag;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea(1, 3)]
        private string _description;

        public GameplayTag Tag => _tag;
        public string DisplayName => _displayName;
        public string Description => _description;

        public TagEntry(GameplayTag tag, string displayName, string description)
        {
            _tag = tag.Value;
            _displayName = displayName;
            _description = description;
        }
    }
}

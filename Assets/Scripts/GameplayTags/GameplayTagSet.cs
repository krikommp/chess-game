using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniChess.GameplayTags
{
    public sealed class GameplayTagSet
    {
        private readonly Dictionary<GameplayTag, HashSet<object>> _entries =
            new Dictionary<GameplayTag, HashSet<object>>();

        public int Count => _entries.Count;
        public IEnumerable<GameplayTag> Tags => _entries.Keys;

        // ── Query ──────────────────────────────────────────────

        public bool Has(GameplayTag tag, TagMatchMode mode = TagMatchMode.Exact)
        {
            if (mode == TagMatchMode.Exact)
                return _entries.ContainsKey(tag);

            foreach (var key in _entries.Keys)
            {
                if (key.Matches(tag, TagMatchMode.Prefix))
                    return true;
            }
            return false;
        }

        public bool HasAny(IEnumerable<GameplayTag> tags, TagMatchMode mode = TagMatchMode.Exact)
        {
            foreach (var tag in tags)
            {
                if (Has(tag, mode)) return true;
            }
            return false;
        }

        public bool HasAll(IEnumerable<GameplayTag> tags, TagMatchMode mode = TagMatchMode.Exact)
        {
            foreach (var tag in tags)
            {
                if (!Has(tag, mode)) return false;
            }
            return true;
        }

        // ── Mutate ─────────────────────────────────────────────

        public void Add(GameplayTag tag, object source = null)
        {
            if (!GameplayTag.IsValid(tag.Value))
                throw new ArgumentException($"Cannot add invalid tag to GameplayTagSet: '{tag.Value}'");

            source ??= TagSourceType.Debug; // default source if none provided

            if (!_entries.TryGetValue(tag, out var sources))
            {
                sources = new HashSet<object>();
                _entries[tag] = sources;
            }
            sources.Add(source);
        }

        public void Remove(GameplayTag tag, object source = null)
        {
            if (!_entries.TryGetValue(tag, out var sources)) return;

            if (source != null)
            {
                sources.Remove(source);
                if (sources.Count == 0)
                    _entries.Remove(tag);
            }
            else
            {
                // Remove all sources for this tag
                _entries.Remove(tag);
            }
        }

        /// <summary>
        /// Remove all tags that were added by the given source (e.g. when a Status expires).
        /// </summary>
        public void RemoveAllFromSource(object source)
        {
            if (source == null) return;

            var toRemove = new List<GameplayTag>();
            foreach (var kvp in _entries)
            {
                kvp.Value.Remove(source);
                if (kvp.Value.Count == 0)
                    toRemove.Add(kvp.Key);
            }
            foreach (var tag in toRemove)
            {
                _entries.Remove(tag);
            }
        }

        public void Clear() => _entries.Clear();

        // ── Debug ──────────────────────────────────────────────

        public override string ToString() =>
            Count == 0 ? "[empty]" : string.Join(", ", _entries.Keys.Select(t => t.Value));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniChess.GameplayTags
{
    public sealed class GameplayTagSet
    {
        private readonly Dictionary<GameplayTag, HashSet<object>> m_entries =
            new Dictionary<GameplayTag, HashSet<object>>();

        public int Count => m_entries.Count;
        public IEnumerable<GameplayTag> Tags => m_entries.Keys;

        // ── Query ──────────────────────────────────────────────

        public bool Has(GameplayTag tag, ETagMatchMode mode = ETagMatchMode.Exact)
        {
            if (mode == ETagMatchMode.Exact)
                return m_entries.ContainsKey(tag);

            foreach (var key in m_entries.Keys)
            {
                if (key.Matches(tag, ETagMatchMode.Prefix))
                    return true;
            }
            return false;
        }

        public bool HasAny(IEnumerable<GameplayTag> tags, ETagMatchMode mode = ETagMatchMode.Exact)
        {
            foreach (var tag in tags)
            {
                if (Has(tag, mode)) return true;
            }
            return false;
        }

        public bool HasAll(IEnumerable<GameplayTag> tags, ETagMatchMode mode = ETagMatchMode.Exact)
        {
            foreach (var tag in tags)
            {
                if (!Has(tag, mode)) return false;
            }
            return true;
        }

        // ── Mutate ─────────────────────────────────────────────

        public bool Add(GameplayTag tag, object source = null)
        {
            if (!GameplayTag.IsValid(tag.Value))
                throw new ArgumentException($"Cannot add invalid tag to GameplayTagSet: '{tag.Value}'");

            source ??= ETagSourceType.Debug;

            bool isNew = !m_entries.ContainsKey(tag);

            if (!m_entries.TryGetValue(tag, out var sources))
            {
                sources = new HashSet<object>();
                m_entries[tag] = sources;
            }
            sources.Add(source);
            return isNew;
        }

        public bool Remove(GameplayTag tag, object source = null)
        {
            if (!m_entries.TryGetValue(tag, out var sources)) return false;

            if (source != null)
            {
                sources.Remove(source);
                if (sources.Count == 0)
                {
                    m_entries.Remove(tag);
                    return true;
                }
            }
            else
            {
                m_entries.Remove(tag);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove all tags that were added by the given source (e.g. when a Status expires).
        /// </summary>
        public void RemoveAllFromSource(object source)
        {
            if (source == null) return;

            var toRemove = new List<GameplayTag>();
            foreach (var kvp in m_entries)
            {
                kvp.Value.Remove(source);
                if (kvp.Value.Count == 0)
                    toRemove.Add(kvp.Key);
            }
            foreach (var tag in toRemove)
            {
                m_entries.Remove(tag);
            }
        }

        public void Clear() => m_entries.Clear();

        // ── Debug ──────────────────────────────────────────────

        public override string ToString() =>
            Count == 0 ? "[empty]" : string.Join(", ", m_entries.Keys.Select(t => t.Value));
    }
}



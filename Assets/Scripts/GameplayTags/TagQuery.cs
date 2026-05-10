using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiniChess.GameplayTags
{
    /// <summary>
    /// Condition query used by skills, AI decisions, and level scripts.
    /// Evaluates against a target's GameplayTagSet.
    /// </summary>
    [Serializable]
    public struct TagQuery
    {
        [SerializeField] private GameplayTagRef[] m_requiredAll;
        [SerializeField] private GameplayTagRef[] m_requiredAny;
        [SerializeField] private GameplayTagRef[] m_blockedAny;
        [SerializeField] private ETagMatchMode m_matchMode;

        public IReadOnlyList<GameplayTagRef> RequiredAll => m_requiredAll ?? Array.Empty<GameplayTagRef>();
        public IReadOnlyList<GameplayTagRef> RequiredAny => m_requiredAny ?? Array.Empty<GameplayTagRef>();
        public IReadOnlyList<GameplayTagRef> BlockedAny => m_blockedAny ?? Array.Empty<GameplayTagRef>();
        public ETagMatchMode MatchMode => m_matchMode;

        public TagQuery(
            GameplayTagRef[] requiredAll = null,
            GameplayTagRef[] requiredAny = null,
            GameplayTagRef[] blockedAny = null,
            ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            m_requiredAll = requiredAll;
            m_requiredAny = requiredAny;
            m_blockedAny = blockedAny;
            m_matchMode = matchMode;
        }

        /// <summary>
        /// Returns true if the target GameplayTagSet passes this query.
        /// </summary>
        public bool Evaluate(GameplayTagSet tagSet)
        {
            if (tagSet == null) return false;

            // Blocked: if target has any blocked tag, fail immediately
            if (BlockedAny.Count > 0)
            {
                var blocked = BlockedAny
                    .Where(r => r.IsValid)
                    .Select(r => (GameplayTag)r)
                    .ToList();
                if (blocked.Count > 0 && tagSet.HasAny(blocked, MatchMode))
                    return false;
            }

            // RequiredAll: all must be present
            foreach (var required in RequiredAll)
            {
                if (!required.IsValid) continue;
                if (!tagSet.Has(required, MatchMode))
                    return false;
            }

            // RequiredAny: at least one must be present (if any specified)
            if (RequiredAny.Count > 0)
            {
                var any = RequiredAny
                    .Where(r => r.IsValid)
                    .Select(r => (GameplayTag)r)
                    .ToList();
                if (any.Count == 0) return true; // no valid requirements → pass
                if (!tagSet.HasAny(any, MatchMode))
                    return false;
            }

            return true;
        }

        public bool IsEmpty =>
            RequiredAll.Count == 0 && RequiredAny.Count == 0 && BlockedAny.Count == 0;
    }
}



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
        [SerializeField] private GameplayTag[] m_requiredAll;
        [SerializeField] private GameplayTag[] m_requiredAny;
        [SerializeField] private GameplayTag[] m_blockedAny;
        [SerializeField] private ETagMatchMode m_matchMode;

        public IReadOnlyList<GameplayTag> RequiredAll => m_requiredAll ?? Array.Empty<GameplayTag>();
        public IReadOnlyList<GameplayTag> RequiredAny => m_requiredAny ?? Array.Empty<GameplayTag>();
        public IReadOnlyList<GameplayTag> BlockedAny => m_blockedAny ?? Array.Empty<GameplayTag>();
        public ETagMatchMode MatchMode => m_matchMode;

        public TagQuery(
            GameplayTag[] requiredAll = null,
            GameplayTag[] requiredAny = null,
            GameplayTag[] blockedAny = null,
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
                var blocked = BlockedAny.Where(t => !string.IsNullOrEmpty(t.Value)).ToArray();
                if (blocked.Length > 0 && tagSet.HasAny(blocked, MatchMode))
                    return false;
            }

            // RequiredAll: all must be present
            foreach (var required in RequiredAll)
            {
                if (string.IsNullOrEmpty(required.Value)) continue;
                if (!tagSet.Has(required, MatchMode))
                    return false;
            }

            // RequiredAny: at least one must be present (if any specified)
            if (RequiredAny.Count > 0)
            {
                var any = RequiredAny.Where(t => !string.IsNullOrEmpty(t.Value)).ToArray();
                if (any.Length == 0) return true;
                if (!tagSet.HasAny(any, MatchMode))
                    return false;
            }

            return true;
        }

        public bool IsEmpty =>
            RequiredAll.Count == 0 && RequiredAny.Count == 0 && BlockedAny.Count == 0;
    }
}



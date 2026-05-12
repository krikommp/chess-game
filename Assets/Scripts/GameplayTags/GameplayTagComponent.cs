using System;
using UnityEngine;

namespace MiniChess.GameplayTags
{
    /// <summary>
    /// Attach to any GameObject that needs a runtime GameplayTagSet.
    /// Provides query/mutate access for skills, effects, AI, and debug output.
    /// </summary>
    public class GameplayTagComponent : MonoBehaviour
    {
        [SerializeField] private GameplayTag[] m_initialTags;

        public GameplayTagSet TagSet { get; private set; } = new GameplayTagSet();

        public event Action<GameplayTag> OnTagAdded;
        public event Action<GameplayTag> OnTagRemoved;

        private void Awake()
        {
            foreach (var tag in m_initialTags)
            {
                if (!string.IsNullOrEmpty(tag.Value))
                {
                    TagSet.Add(tag, ETagSourceType.Debug);
                }
            }
        }

        public bool HasTag(GameplayTag tag, ETagMatchMode mode = ETagMatchMode.Exact) =>
            TagSet.Has(tag, mode);

        public bool HasAnyTag(GameplayTag[] tags, ETagMatchMode mode = ETagMatchMode.Exact) =>
            TagSet.HasAny(tags, mode);

        public bool HasAllTags(GameplayTag[] tags, ETagMatchMode mode = ETagMatchMode.Exact) =>
            TagSet.HasAll(tags, mode);

        public void AddTag(GameplayTag tag, object source = null)
        {
            bool added = TagSet.Add(tag, source ?? ETagSourceType.Debug);
            if (added)
                OnTagAdded?.Invoke(tag);
        }

        public void RemoveTag(GameplayTag tag, object source = null)
        {
            bool removed = TagSet.Remove(tag, source);
            if (removed)
                OnTagRemoved?.Invoke(tag);
        }

        public void RemoveAllTagsFromSource(object source) =>
            TagSet.RemoveAllFromSource(source);

        public override string ToString() =>
            $"[{name}] {TagSet}";
    }
}



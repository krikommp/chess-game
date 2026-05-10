using UnityEngine;

namespace MiniChess.GameplayTags
{
    /// <summary>
    /// Attach to any GameObject that needs a runtime GameplayTagSet.
    /// Provides query/mutate access for skills, effects, AI, and debug output.
    /// </summary>
    public class GameplayTagComponent : MonoBehaviour
    {
        [SerializeField] private GameplayTagRef[] m_initialTags;

        public GameplayTagSet TagSet { get; private set; } = new GameplayTagSet();

        private void Awake()
        {
            foreach (var tagRef in m_initialTags)
            {
                if (tagRef.TryGetTag(out var tag))
                {
                    TagSet.Add(tag, ETagSourceType.Debug);
                }
            }
        }

        // Convenience shortcuts so callers don't need to reach into TagSet directly
        public bool HasTag(GameplayTag tag, ETagMatchMode mode = ETagMatchMode.Exact) =>
            TagSet.Has(tag, mode);

        public bool HasAnyTag(GameplayTag[] tags, ETagMatchMode mode = ETagMatchMode.Exact) =>
            TagSet.HasAny(tags, mode);

        public bool HasAllTags(GameplayTag[] tags, ETagMatchMode mode = ETagMatchMode.Exact) =>
            TagSet.HasAll(tags, mode);

        public void AddTag(GameplayTag tag, object source = null) =>
            TagSet.Add(tag, source ?? ETagSourceType.Debug);

        public void RemoveTag(GameplayTag tag, object source = null) =>
            TagSet.Remove(tag, source);

        public void RemoveAllTagsFromSource(object source) =>
            TagSet.RemoveAllFromSource(source);

        public override string ToString() =>
            $"[{name}] {TagSet}";
    }
}



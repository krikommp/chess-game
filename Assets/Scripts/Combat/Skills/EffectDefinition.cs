using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public abstract class EffectDefinition : ScriptableObject
    {
        [SerializeField] private GameplayTags.GameplayTagRef[] m_tags;
        [SerializeField, TextArea(1, 3)] private string m_description;

        public GameplayTags.GameplayTagRef[] Tags => m_tags ?? System.Array.Empty<GameplayTags.GameplayTagRef>();
        public string Description => m_description ?? string.Empty;

        public abstract ETargetCapability RequiredCapability { get; }

        public abstract void Apply(EffectContext context);

        public bool HasAnyTag()
        {
            var all = Tags;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].IsValid)
                    return true;
            }
            return false;
        }
    }
}


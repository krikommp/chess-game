using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public abstract class EffectDefinition : ScriptableObject
    {
        [SerializeField] private GameplayTags.GameplayTag[] m_tags;
        [SerializeField, TextArea(1, 3)] private string m_description;

        public GameplayTags.GameplayTag[] Tags => m_tags ?? System.Array.Empty<GameplayTags.GameplayTag>();
        public string Description => m_description ?? string.Empty;

        public abstract ETargetCapability RequiredCapability { get; }

        public abstract void Apply(EffectContext context);

        public bool HasAnyTag()
        {
            var all = Tags;
            for (int i = 0; i < all.Length; i++)
            {
                if (!string.IsNullOrEmpty(all[i].Value))
                    return true;
            }
            return false;
        }
    }
}


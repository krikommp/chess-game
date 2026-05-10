using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public abstract class EffectDefinition : ScriptableObject
    {
        [SerializeField] private GameplayTags.GameplayTagRef[] _tags;
        [SerializeField, TextArea(1, 3)] private string _description;

        public GameplayTags.GameplayTagRef[] Tags => _tags ?? System.Array.Empty<GameplayTags.GameplayTagRef>();
        public string Description => _description ?? string.Empty;

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

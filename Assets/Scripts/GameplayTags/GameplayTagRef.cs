using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiniChess.GameplayTags
{
    /// <summary>
    /// Serializable reference to a GameplayTag for use in config assets (ScriptableObject, MonoBehaviour).
    /// Use this instead of a raw string field so the tag is validated on edit.
    /// </summary>
    [Serializable]
    public struct GameplayTagRef
    {
        [SerializeField] private string m_value;

        public string Value => m_value ?? string.Empty;

        public GameplayTagRef(string value)
        {
            m_value = value;
        }

        public GameplayTag ToTag()
        {
            if (!GameplayTag.IsValid(Value))
                throw new InvalidOperationException($"Invalid GameplayTagRef: '{Value}'");
            return new GameplayTag(Value);
        }

        public bool TryGetTag(out GameplayTag tag)
        {
            if (GameplayTag.IsValid(Value))
            {
                tag = new GameplayTag(Value);
                return true;
            }
            tag = default;
            return false;
        }

        public bool IsValid => GameplayTag.IsValid(Value);

        public static implicit operator GameplayTagRef(string value) => new(value);
        public static implicit operator GameplayTag(GameplayTagRef r) => r.ToTag();
    }
}


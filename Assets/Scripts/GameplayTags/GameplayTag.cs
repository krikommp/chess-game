using System;
using UnityEngine;

namespace MiniChess.GameplayTags
{
    [Serializable]
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        [SerializeField] private readonly string m_value;

        public string Value => m_value ?? string.Empty;

        public GameplayTag(string value)
        {
            if (!IsValid(value))
            {
                throw new ArgumentException($"Invalid GameplayTag: '{value}'");
            }
            m_value = value;
        }

        public bool Equals(GameplayTag other) =>
            string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj) =>
            obj is GameplayTag other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Equals(b);
        public static bool operator !=(GameplayTag a, GameplayTag b) => !a.Equals(b);

        public static implicit operator GameplayTag(string value) => new(value);


        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (value.StartsWith(".") || value.EndsWith(".")) return false;
            if (value.Contains("..")) return false;
            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c)) return false;
            }
            return true;
        }

        /// <summary>
        /// Match this tag against a query tag. Exact: values must be identical.
        /// Prefix: query segments must be a prefix of this tag's segments, on segment boundaries.
        /// </summary>
        public bool Matches(GameplayTag query, ETagMatchMode mode)
        {
            if (mode == ETagMatchMode.Exact)
            {
                return Equals(query);
            }

            // Prefix: query segments must be a prefix of this tag's segments
            string[] queryParts = query.Value.Split('.');
            string[] targetParts = Value.Split('.');

            if (queryParts.Length > targetParts.Length) return false;

            for (int i = 0; i < queryParts.Length; i++)
            {
                if (!string.Equals(queryParts[i], targetParts[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
    }
}



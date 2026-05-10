using System;
using UnityEngine;

namespace MiniChess.GameplayTags
{
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        [SerializeField] private string m_value;
        [SerializeField] private int m_id;

        public string Value => m_value ?? string.Empty;

        // Falls back to computing hash from m_value when m_id is 0 (old serialized data).
        private int Id => m_id != 0 ? m_id : ComputeTagHash(m_value);

        // FNV-1a hash, case-insensitive.
        public static int ComputeTagHash(string value)
        {
            unchecked
            {
                int hash = (int)2166136261;
                foreach (char c in value)
                {
                    hash ^= char.ToLowerInvariant(c);
                    hash *= 16777619;
                }
                return hash;
            }
        }

        public GameplayTag(string value)
        {
            if (!IsValid(value))
            {
                throw new ArgumentException($"Invalid GameplayTag: '{value}'");
            }
            m_value = value;
            m_id = ComputeTagHash(value);
        }

        internal GameplayTag(int precomputedHash, string debugName)
        {
            m_value = debugName;
            m_id = precomputedHash;
        }

        public bool Equals(GameplayTag other) => Id == other.Id;

        public override bool Equals(object obj) =>
            obj is GameplayTag other && Equals(other);

        public override int GetHashCode() => Id;

        public override string ToString() => Value;

        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Id == b.Id;
        public static bool operator !=(GameplayTag a, GameplayTag b) => a.Id != b.Id;

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
        /// Match this tag against a query tag. Exact: hash comparison.
        /// Prefix: query segments must be a prefix of this tag's segments, on segment boundaries.
        /// </summary>
        public bool Matches(GameplayTag query, ETagMatchMode mode)
        {
            if (mode == ETagMatchMode.Exact)
            {
                return Id == query.Id;
            }

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

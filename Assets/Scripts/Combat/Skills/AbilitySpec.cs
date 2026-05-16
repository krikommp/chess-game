using System;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [Serializable]
    public sealed class AbilitySpec
    {
        [SerializeField] private SkillDefinition m_definition;
        [SerializeField] private GameObject m_source;
        [SerializeField, Min(1)] private int m_level = 1;

        [NonSerialized] private object m_grantSource;

        public AbilitySpec(SkillDefinition definition, GameObject source = null, object grantSource = null, int level = 1)
        {
            m_definition = definition;
            m_source = source;
            m_grantSource = grantSource;
            m_level = Mathf.Max(1, level);
        }

        public SkillDefinition Definition => m_definition;
        public SkillAbility Ability => m_definition != null ? m_definition.Ability : null;
        public GameObject Source => m_source;
        public object GrantSource => m_grantSource;
        public int Level => Mathf.Max(1, m_level);

        public string Id
        {
            get
            {
                if (m_definition != null)
                    return m_definition.Id;

                return string.Empty;
            }
        }

        public SkillMetadata? Metadata => m_definition != null ? m_definition.Metadata : null;

        public bool IsValid => Ability != null;

        public static AbilitySpec FromDefinition(
            SkillDefinition definition,
            GameObject source = null,
            object grantSource = null,
            int level = 1)
        {
            return new AbilitySpec(definition, source, grantSource, level);
        }
    }
}

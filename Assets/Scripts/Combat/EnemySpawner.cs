using MiniChess.GameplayTags;
using MiniChess.Combat.Skills;
using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Place in scene to spawn an enemy at this position on Start.
    /// Self-destructs after spawning.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Enemy Config")]
        [SerializeField] private string m_enemyName = "Enemy";
        [SerializeField] private int m_initiative = 5;
        [SerializeField] private int m_hp = 100;

        [Header("Visual")]
        [SerializeField] private Color m_enemyColor = new Color(0.7f, 0.2f, 0.2f);

        [Header("Skills")]
        [Tooltip("Skills assigned to the spawned enemy unit. MVP test scenes should assign basic_move here.")]
        [SerializeField] private SkillDefinition[] m_defaultSkills;

        private void Awake()
        {
            SpawnEnemy();
            Destroy(gameObject);
        }

        private void SpawnEnemy()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = m_enemyName;
            go.transform.position = transform.position;
            go.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);

            // Set color
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.color = m_enemyColor;
            }

            // Component stack: CombatUnit → AttributeSet → MovementController → AbilitySystemComponent
            go.AddComponent<CombatUnit>();
            var tagComp = go.AddComponent<GameplayTagComponent>();
            tagComp.AddTag(new GameplayTags.GameplayTag("Control.AI"), "Auto-assigned by EnemySpawner");
            tagComp.AddTag(new GameplayTags.GameplayTag("Faction.Enemy"), "Auto-assigned by EnemySpawner");

            var attr = go.AddComponent<AttributeSet>();
            attr.Testing_AddAttribute(WellKnownAttributeTags.HP, m_hp, m_hp);
            attr.Testing_AddAttribute(WellKnownAttributeTags.AP, 6f, 6f);
            attr.Testing_AddAttribute(WellKnownAttributeTags.Initiative, m_initiative, 0f);
            attr.Testing_AddAttribute(WellKnownAttributeTags.MoveSpeed, 2f, 0f);
            attr.DisplayName = m_enemyName;
            attr.OverrideFactionForTesting(EFaction.Enemy);

            go.AddComponent<MovementController>();

            AbilitySystemComponent skillExecutor = go.AddComponent<AbilitySystemComponent>();
            skillExecutor.SetSkillDefinitions(m_defaultSkills);
        }
    }
}

using UnityEngine;
using MiniChess.Combat.Skills;

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

            // New component stack: AttributeSet → MovementController → SkillExecutor → EnemyController
            var attr = go.AddComponent<AttributeSet>();
            attr.Testing_AddAttribute(WellKnownAttributeTags.HP, m_hp, m_hp);
            attr.Testing_AddAttribute(WellKnownAttributeTags.AP, 6f, 6f);
            attr.Testing_AddAttribute(WellKnownAttributeTags.Initiative, m_initiative, 0f);
            attr.Testing_AddAttribute(WellKnownAttributeTags.MoveSpeed, 2f, 0f);
            attr.DisplayName = m_enemyName;
            attr.OverrideFactionForTesting(EFaction.Enemy);

            go.AddComponent<MovementController>();

            SkillExecutor skillExecutor = go.AddComponent<SkillExecutor>();
            skillExecutor.SetSkills(m_defaultSkills);

            // Enemy controller (bridge phase — syncs from AttributeSet)
            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.DefaultColor = m_enemyColor;

            // TODO(Docs/06_MAP_SPEC.md §2): Revisit dynamic unit blocking once
            // enemy AI movement and obstacle carving share a proper movement layer.
            // Do not add NavMeshObstacle in the MVP AI loop because it conflicts
            // with the NavMeshAgent required by EnemyController.
        }
    }
}

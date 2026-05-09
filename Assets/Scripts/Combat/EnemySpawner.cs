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
        public string enemyName = "Enemy";
        public int initiative = 5;
        public int hp = 100;

        [Header("Visual")]
        public Color enemyColor = new Color(0.7f, 0.2f, 0.2f);

        private void Awake()
        {
            SpawnEnemy();
            Destroy(gameObject);
        }

        private void SpawnEnemy()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = enemyName;
            go.transform.position = transform.position;
            go.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);

            // Set color
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.color = enemyColor;
            }

            // Enemy controller
            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.displayName = enemyName;
            enemy.initiative = initiative;
            enemy.maxHP = hp;
            enemy.defaultColor = enemyColor;

            // TODO(Docs/06_MAP_SPEC.md §2): Revisit dynamic unit blocking once
            // enemy AI movement and obstacle carving share a proper movement layer.
            // Do not add NavMeshObstacle in the MVP AI loop because it conflicts
            // with the NavMeshAgent required by EnemyController.
        }
    }
}

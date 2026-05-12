using MiniChess.Combat;
using MiniChess.Combat.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniChess.EditorTools
{
    public static class CreateDefaultSkills
    {
        private const string BasicMovePath = "Assets/Data/Skills/basic_move.asset";

        [MenuItem("MiniChess/Create basic_move Skill")]
        public static void CreateBasicMove()
        {
            if (AssetDatabase.LoadAssetAtPath<SkillAbility>(BasicMovePath) != null)
            {
                Debug.Log("[CreateDefaultSkills] basic_move.asset already exists.");
                return;
            }

            EnsureDirectoryExists(BasicMovePath);

            var skill = ScriptableObject.CreateInstance<GroundMoveAbility>();
            ConfigureBasicMove(skill);

            AssetDatabase.CreateAsset(skill, BasicMovePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CreateDefaultSkills] Created basic_move.asset at {BasicMovePath}");
        }

        [MenuItem("MiniChess/Configure Current Scene Move Skills")]
        public static void ConfigureCurrentSceneMoveSkills()
        {
            var moveSkill = AssetDatabase.LoadAssetAtPath<GroundMoveAbility>(BasicMovePath);
            if (moveSkill == null)
            {
                moveSkill = ScriptableObject.CreateInstance<GroundMoveAbility>();
                ConfigureBasicMove(moveSkill);
                EnsureDirectoryExists(BasicMovePath);
                AssetDatabase.CreateAsset(moveSkill, BasicMovePath);
            }
            else
            {
                ConfigureBasicMove(moveSkill);
                EditorUtility.SetDirty(moveSkill);
            }

            int configuredCount = 0;
            configuredCount += ConfigurePlayerMoveSkill("Gameplay/Actors/player1", moveSkill);
            configuredCount += ConfigurePlayerMoveSkill("Gameplay/Actors/player2", moveSkill);
            configuredCount += ConfigurePlayerMoveSkill("Gameplay/Actors/player3", moveSkill);
            configuredCount += ConfigurePlayerMoveSkill("Gameplay/Actors/player4", moveSkill);
            int configuredSpawnerCount = ConfigureEnemySpawners(moveSkill);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[CreateDefaultSkills] Configured basic_move for {configuredCount} player actor(s) and {configuredSpawnerCount} enemy spawner(s).");
        }

        private static void ConfigureBasicMove(GroundMoveAbility skill)
        {
            var so = new SerializedObject(skill);

            so.FindProperty("m_id").stringValue = "basic_move";
            so.FindProperty("m_displayName").stringValue = "Basic Move";
            so.FindProperty("m_description").stringValue = "Move to a target position.";

            so.ApplyModifiedProperties();
        }

        private static int ConfigurePlayerMoveSkill(string hierarchyPath, SkillAbility moveSkill)
        {
            var actor = GameObject.Find(hierarchyPath);
            if (actor == null)
                return 0;

            var executor = actor.GetComponent<SkillExecutor>();
            if (executor == null)
                executor = actor.AddComponent<SkillExecutor>();

            var so = new SerializedObject(executor);
            var skillsProp = so.FindProperty("m_availableSkills");
            skillsProp.arraySize = 1;
            skillsProp.GetArrayElementAtIndex(0).objectReferenceValue = moveSkill;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(actor);
            EditorUtility.SetDirty(executor);
            return 1;
        }

        private static int ConfigureEnemySpawners(SkillAbility moveSkill)
        {
            var spawners = Object.FindObjectsOfType<EnemySpawner>(includeInactive: true);
            for (int i = 0; i < spawners.Length; i++)
            {
                var so = new SerializedObject(spawners[i]);
                var skillsProp = so.FindProperty("m_defaultSkills");
                skillsProp.arraySize = 1;
                skillsProp.GetArrayElementAtIndex(0).objectReferenceValue = moveSkill;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(spawners[i]);
            }

            return spawners.Length;
        }

        private static void EnsureDirectoryExists(string assetPath)
        {
            var dir = System.IO.Path.GetDirectoryName(assetPath);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
        }
    }
}

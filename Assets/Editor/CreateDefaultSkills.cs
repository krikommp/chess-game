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
        private const string GroundMoveAbilityPath = "Assets/Data/Skills/GroundMoveAbility.asset";

        [MenuItem("MiniChess/Create basic_move Skill")]
        public static void CreateBasicMove()
        {
            if (AssetDatabase.LoadAssetAtPath<SkillDefinition>(BasicMovePath) != null)
            {
                Debug.Log("[CreateDefaultSkills] basic_move.asset already exists.");
                return;
            }

            EnsureDirectoryExists(BasicMovePath);

            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            ConfigureBasicMoveSkill(skill);

            AssetDatabase.CreateAsset(skill, BasicMovePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CreateDefaultSkills] Created basic_move.asset at {BasicMovePath}");
        }

        [MenuItem("MiniChess/Configure Current Scene Move Skills")]
        public static void ConfigureCurrentSceneMoveSkills()
        {
            var moveSkill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(BasicMovePath);
            if (moveSkill == null)
            {
                moveSkill = ScriptableObject.CreateInstance<SkillDefinition>();
                ConfigureBasicMoveSkill(moveSkill);
                EnsureDirectoryExists(BasicMovePath);
                AssetDatabase.CreateAsset(moveSkill, BasicMovePath);
            }
            else
            {
                ConfigureBasicMoveSkill(moveSkill);
                EditorUtility.SetDirty(moveSkill);
            }

            var groundMoveAbility = AssetDatabase.LoadAssetAtPath<GroundMoveAbility>(GroundMoveAbilityPath);
            if (groundMoveAbility == null)
            {
                groundMoveAbility = ScriptableObject.CreateInstance<GroundMoveAbility>();
                EnsureDirectoryExists(GroundMoveAbilityPath);
                AssetDatabase.CreateAsset(groundMoveAbility, GroundMoveAbilityPath);
            }

            var moveSkillObject = new SerializedObject(moveSkill);
            moveSkillObject.FindProperty("m_ability").objectReferenceValue = groundMoveAbility;
            moveSkillObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(moveSkill);

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

        private static void ConfigureBasicMoveSkill(SkillDefinition skill)
        {
            var so = new SerializedObject(skill);

            so.FindProperty("m_id").stringValue = "basic_move";
            so.FindProperty("m_displayName").stringValue = "Basic Move";
            so.FindProperty("m_description").stringValue = "Move to a target position.";
            so.FindProperty("m_apCost").intValue = 0;
            so.FindProperty("m_cooldown").intValue = 0;
            so.FindProperty("m_range").floatValue = 0f;
            so.FindProperty("m_targetType").enumValueIndex = (int)ESkillTargetType.GroundPoint;
            so.FindProperty("m_aiBaseWeight").floatValue = 10f;

            // skillTags: Action.Move, Movement.Ground
            var tagsProp = so.FindProperty("m_skillTags");
            tagsProp.arraySize = 2;
            tagsProp.GetArrayElementAtIndex(0).FindPropertyRelative("m_value").stringValue = "Action.Move";
            tagsProp.GetArrayElementAtIndex(1).FindPropertyRelative("m_value").stringValue = "Movement.Ground";

            // aiTags: AI.Skill.Mobility
            var aiTagsProp = so.FindProperty("m_aiTags");
            aiTagsProp.arraySize = 1;
            aiTagsProp.GetArrayElementAtIndex(0).FindPropertyRelative("m_value").stringValue = "AI.Skill.Mobility";

            so.ApplyModifiedProperties();
        }

        private static int ConfigurePlayerMoveSkill(string hierarchyPath, SkillDefinition moveSkill)
        {
            var actor = GameObject.Find(hierarchyPath);
            if (actor == null)
                return 0;

            var executor = actor.GetComponent<SkillExecutor>();
            if (executor == null)
                executor = actor.AddComponent<SkillExecutor>();

            var so = new SerializedObject(executor);
            var skillsProp = so.FindProperty("availableSkills");
            skillsProp.arraySize = 1;
            skillsProp.GetArrayElementAtIndex(0).objectReferenceValue = moveSkill;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(actor);
            EditorUtility.SetDirty(executor);
            return 1;
        }

        private static int ConfigureEnemySpawners(SkillDefinition moveSkill)
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

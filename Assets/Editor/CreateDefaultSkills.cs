using MiniChess.Combat;
using MiniChess.Combat.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniChess.EditorTools
{
    public static class CreateDefaultSkills
    {
        private const string BasicMoveDefinitionPath = "Assets/Data/Skills/basic_move.asset";
        private const string BasicMoveAbilityPath = "Assets/Data/SkillAbilities/basic_move_ability.asset";

        [MenuItem("MiniChess/Create basic_move Skill")]
        public static void CreateBasicMove()
        {
            var ability = GetOrCreateBasicMoveAbility();
            var definition = GetOrCreateBasicMoveDefinition(ability);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CreateDefaultSkills] basic_move SkillDefinition ready at {AssetDatabase.GetAssetPath(definition)}");
        }

        [MenuItem("MiniChess/Configure Current Scene Move Skills")]
        public static void ConfigureCurrentSceneMoveSkills()
        {
            var ability = GetOrCreateBasicMoveAbility();
            var definition = GetOrCreateBasicMoveDefinition(ability);

            int configuredCount = 0;
            configuredCount += ConfigurePlayerMoveSkill("Gameplay/Actors/player1", definition);
            configuredCount += ConfigurePlayerMoveSkill("Gameplay/Actors/player2", definition);
            configuredCount += ConfigurePlayerMoveSkill("Gameplay/Actors/player3", definition);
            configuredCount += ConfigurePlayerMoveSkill("Gameplay/Actors/player4", definition);
            int configuredSpawnerCount = ConfigureEnemySpawners(definition);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[CreateDefaultSkills] Configured basic_move for {configuredCount} player actor(s) and {configuredSpawnerCount} enemy spawner(s).");
        }

        private static GroundMoveAbility GetOrCreateBasicMoveAbility()
        {
            var ability = AssetDatabase.LoadAssetAtPath<GroundMoveAbility>(BasicMoveAbilityPath);
            if (ability == null)
            {
                ability = ScriptableObject.CreateInstance<GroundMoveAbility>();
                ability.name = "basic_move_ability";
                EnsureDirectoryExists(BasicMoveAbilityPath);
                AssetDatabase.CreateAsset(ability, BasicMoveAbilityPath);
            }

            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static SkillDefinition GetOrCreateBasicMoveDefinition(GroundMoveAbility ability)
        {
            var definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>(BasicMoveDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<SkillDefinition>();
                definition.name = "basic_move";
                EnsureDirectoryExists(BasicMoveDefinitionPath);
                AssetDatabase.CreateAsset(definition, BasicMoveDefinitionPath);
            }

            ConfigureBasicMoveDefinition(definition, ability);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ConfigureBasicMoveDefinition(SkillDefinition definition, GroundMoveAbility ability)
        {
            var so = new SerializedObject(definition);

            so.FindProperty("m_id").stringValue = "basic_move";
            so.FindProperty("m_displayName").stringValue = "Basic Move";
            so.FindProperty("m_description").stringValue = "Move to a target position.";
            so.FindProperty("m_targetType").enumValueIndex = (int)ESkillTargetType.GroundPoint;
            so.FindProperty("m_ability").objectReferenceValue = ability;

            so.ApplyModifiedProperties();
        }

        private static int ConfigurePlayerMoveSkill(string hierarchyPath, SkillDefinition moveSkill)
        {
            var actor = GameObject.Find(hierarchyPath);
            if (actor == null)
                return 0;

            var executor = actor.GetComponent<AbilitySystemComponent>();
            if (executor == null)
                executor = actor.AddComponent<AbilitySystemComponent>();

            var so = new SerializedObject(executor);
            var skillsProp = so.FindProperty("m_availableSkills");
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

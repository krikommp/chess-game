using MiniChess.Combat;
using MiniChess.Combat.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniChess.Editor
{
    public static class AttributeStackMigrationTool
    {
        private const string k_MenuPath = "MiniChess/Migrate Scene Units To Attribute Stack";
        private const string k_PlayerAttributeSetPath = "Assets/Data/Attributes/player_attribute_set.asset";
        private const string k_BasicMoveSkillPath = "Assets/Data/Skills/basic_move.asset";
        private const string k_AttributeFolderPath = "Assets/Data/Attributes";

        [MenuItem(k_MenuPath)]
        public static void MigrateSceneUnits()
        {
            EnsureFolder(k_AttributeFolderPath);

            AttributeSetDef playerDefinition = LoadOrCreatePlayerDefinition();
            SkillDefinition basicMoveSkill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(k_BasicMoveSkillPath);
            int migratedCount = 0;

            foreach (Player1Controller player in Object.FindObjectsOfType<Player1Controller>(true))
            {
                bool migrated = EnsureUnitStack(player.gameObject, playerDefinition, EFaction.Player, player.gameObject.name);
                EnsureSkill(player.gameObject, basicMoveSkill);
                migratedCount += migrated ? 1 : 0;
            }

            foreach (EnemyController enemy in Object.FindObjectsOfType<EnemyController>(true))
            {
                migratedCount += EnsureUnitStack(enemy.gameObject, playerDefinition, EFaction.Enemy, enemy.gameObject.name) ? 1 : 0;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            Debug.Log($"[MiniChess] Attribute stack migration complete. Units processed: {migratedCount}.");
        }

        private static bool EnsureUnitStack(GameObject unit, AttributeSetDef definition, EFaction faction, string displayName)
        {
            if (unit == null)
            {
                return false;
            }

            AttributeSet attributes = unit.GetComponent<AttributeSet>();
            if (attributes == null)
            {
                attributes = Undo.AddComponent<AttributeSet>(unit);
            }

            MovementController movement = unit.GetComponent<MovementController>();
            if (movement == null)
            {
                Undo.AddComponent<MovementController>(unit);
            }

            SerializedObject serializedAttributes = new SerializedObject(attributes);
            serializedAttributes.FindProperty("m_definition").objectReferenceValue = definition;
            serializedAttributes.FindProperty("m_displayName").stringValue = displayName;
            serializedAttributes.FindProperty("m_faction").enumValueIndex = (int)faction;
            serializedAttributes.ApplyModifiedProperties();

            EditorUtility.SetDirty(attributes);
            EditorUtility.SetDirty(unit);
            return true;
        }

        private static void EnsureSkill(GameObject unit, SkillDefinition skill)
        {
            if (unit == null || skill == null)
            {
                return;
            }

            SkillExecutor executor = unit.GetComponent<SkillExecutor>();
            if (executor == null)
            {
                return;
            }

            SerializedObject serializedExecutor = new SerializedObject(executor);
            SerializedProperty availableSkills = serializedExecutor.FindProperty("m_availableSkills");
            if (availableSkills == null)
            {
                return;
            }

            for (int i = 0; i < availableSkills.arraySize; i++)
            {
                if (availableSkills.GetArrayElementAtIndex(i).objectReferenceValue == skill)
                {
                    return;
                }
            }

            int nextIndex = availableSkills.arraySize;
            availableSkills.arraySize++;
            availableSkills.GetArrayElementAtIndex(nextIndex).objectReferenceValue = skill;
            serializedExecutor.ApplyModifiedProperties();
            EditorUtility.SetDirty(executor);
        }

        private static AttributeSetDef LoadOrCreatePlayerDefinition()
        {
            AttributeSetDef definition = AssetDatabase.LoadAssetAtPath<AttributeSetDef>(k_PlayerAttributeSetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<AttributeSetDef>();
                AssetDatabase.CreateAsset(definition, k_PlayerAttributeSetPath);
            }

            SerializedObject serializedDefinition = new SerializedObject(definition);
            SerializedProperty entries = serializedDefinition.FindProperty("m_entries");
            entries.arraySize = 4;

            SetEntry(entries.GetArrayElementAtIndex(0), WellKnownAttributeTags.HP.Value, 100f, 100f);
            SetEntry(entries.GetArrayElementAtIndex(1), WellKnownAttributeTags.AP.Value, 6f, 6f);
            SetEntry(entries.GetArrayElementAtIndex(2), WellKnownAttributeTags.Initiative.Value, 10f, 0f);
            SetEntry(entries.GetArrayElementAtIndex(3), WellKnownAttributeTags.MoveSpeed.Value, 2f, 0f);

            serializedDefinition.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void SetEntry(SerializedProperty entry, string tag, float baseValue, float maxValue)
        {
            entry.FindPropertyRelative("Tag").FindPropertyRelative("m_value").stringValue = tag;
            entry.FindPropertyRelative("BaseValue").floatValue = baseValue;
            entry.FindPropertyRelative("MaxValue").floatValue = maxValue;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}

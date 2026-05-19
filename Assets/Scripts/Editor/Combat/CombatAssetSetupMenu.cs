using MiniChess.Combat.Skills;
using MiniChess.GameplayTags;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniChess.Editor.Combat
{
    public static class CombatAssetSetupMenu
    {
        private const string k_EffectFunctionsFolder = "Assets/Data/EffectFunctions";
        private const string k_EffectsFolder = "Assets/Data/Effects";
        private const string k_SkillsFolder = "Assets/Data/Skills";
        private const string k_MoveAbilityPath = k_SkillsFolder + "/GroundMoveAbility.asset";
        private const string k_MoveDefinitionPath = k_SkillsFolder + "/basic_move.asset";
        private const string k_PlayerAttributesPath = "Assets/Data/Attributes/player_attribute_set.asset";

        [MenuItem("MiniChess/Combat Config/Ensure Basic Move Assets")]
        public static void EnsureBasicMoveAssets()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(k_EffectFunctionsFolder);
            EnsureFolder(k_EffectsFolder);
            EnsureFolder(k_SkillsFolder);

            var moveAbility = EnsureAsset<GroundMoveAbility>(k_MoveAbilityPath);
            var moveDefinition = EnsureAsset<SkillDefinition>(k_MoveDefinitionPath);
            DeleteIfExists(k_SkillsFolder + "/basic_move_definition.asset");

            SetStringField(moveDefinition, "m_id", "basic_move");
            SetStringField(moveDefinition, "m_displayName", "Basic Move");
            SetStringField(moveDefinition, "m_description", "Move to a target position.");
            SetEnumField(moveDefinition, "m_targetType", ESkillTargetType.GroundPoint);
            SetObjectField(moveDefinition, "m_ability", moveAbility);

            SetObjectArrayField(moveAbility, "m_costs", System.Array.Empty<Object>());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MiniChess] Ensured basic_move SkillDefinition and GroundMoveAbility assets.");
        }

        [MenuItem("MiniChess/Combat Config/Ensure Sample Player Move Scene")]
        public static void EnsureSamplePlayerMoveScene()
        {
            EnsureBasicMoveAssets();

            var moveDefinition = AssetDatabase.LoadAssetAtPath<SkillDefinition>(k_MoveDefinitionPath);
            var playerAttributes = AssetDatabase.LoadAssetAtPath<MiniChess.Combat.AttributeSetDef>(k_PlayerAttributesPath);
            if (moveDefinition == null || playerAttributes == null)
            {
                Debug.LogError("[MiniChess] Missing basic_move or player AttributeSetDef asset.");
                return;
            }

            ConfigurePlayerUnit("Gameplay/Actors/player1", "Player 1", playerAttributes, moveDefinition);
            ConfigurePlayerUnit("Gameplay/Actors/player2", "Player 2", playerAttributes, moveDefinition);
            ConfigurePlayerUnit("Gameplay/Actors/player3", "Player 3", playerAttributes, moveDefinition);
            ConfigureUnitTurnHandler();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[MiniChess] Ensured SampleScene player movement components and explicit skill references.");
        }

        private static void ConfigurePlayerUnit(
            string path,
            string displayName,
            MiniChess.Combat.AttributeSetDef attributes,
            SkillDefinition moveDefinition)
        {
            var go = GameObject.Find(path);
            if (go == null)
            {
                Debug.LogWarning($"[MiniChess] Could not find player unit at {path}.");
                return;
            }

            var attr = EnsureComponent<MiniChess.Combat.AttributeSet>(go);
            var tagComp = EnsureComponent<GameplayTagComponent>(go);
            var asc = EnsureComponent<AbilitySystemComponent>(go);
            EnsureComponent<MiniChess.Combat.CombatUnit>(go);
            EnsureComponent<MiniChess.Combat.MovementController>(go);

            SetObjectField(attr, "m_definition", attributes);
            SetStringField(attr, "m_displayName", displayName);
            SetEnumField(attr, "m_faction", MiniChess.Combat.EFaction.Player);

            SetGameplayTagArrayField(tagComp, "m_initialTags", new[]
            {
                new GameplayTag("Control.Human"),
                new GameplayTag("Faction.Player"),
            });

            SetObjectArrayField(asc, "m_availableSkills", new Object[] { moveDefinition });
            EditorUtility.SetDirty(go);
        }

        private static void ConfigureUnitTurnHandler()
        {
            var managerObject = GameObject.Find("Managers/[CombatRoundManager]");
            var inputObject = GameObject.Find("Systems/[InputRoot]");
            var cameraObject = GameObject.Find("Systems/Main Camera");
            if (managerObject == null)
            {
                Debug.LogWarning("[MiniChess] Could not find Managers/[CombatRoundManager].");
                return;
            }

            var handler = EnsureComponent<MiniChess.Combat.UnitTurnHandler>(managerObject);
            var roundManager = managerObject.GetComponent<MiniChess.Combat.CombatRoundManager>();
            var inputController = inputObject != null ? inputObject.GetComponent<MiniChess.Combat.InputController>() : null;
            var cameraController = cameraObject != null ? cameraObject.GetComponent<CameraController>() : null;

            SetObjectField(handler, "m_roundManager", roundManager);
            SetObjectField(handler, "m_inputController", inputController);
            SetObjectField(handler, "m_cameraController", cameraController);
            EditorUtility.SetDirty(managerObject);
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                AssetDatabase.DeleteAsset(path);

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void DeleteIfExists(string path)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                AssetDatabase.DeleteAsset(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int split = path.LastIndexOf('/');
            string parent = path.Substring(0, split);
            string folder = path.Substring(split + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component != null)
                return component;

            component = go.AddComponent<T>();
            EditorUtility.SetDirty(go);
            return component;
        }

        private static void SetStringField(Object target, string fieldName, string value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[MiniChess] Missing serialized field {fieldName} on {target.name}.");
                return;
            }

            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetEnumField<TEnum>(Object target, string fieldName, TEnum value)
            where TEnum : System.Enum
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[MiniChess] Missing serialized field {fieldName} on {target.name}.");
                return;
            }

            property.enumValueIndex = System.Convert.ToInt32(value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetObjectField(Object target, string fieldName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[MiniChess] Missing serialized field {fieldName} on {target.name}.");
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetObjectArrayField(Object target, string fieldName, Object[] values)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                Debug.LogWarning($"[MiniChess] Missing serialized array field {fieldName} on {target.name}.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetGameplayTagArrayField(Object target, string fieldName, GameplayTag[] values)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                Debug.LogWarning($"[MiniChess] Missing serialized GameplayTag array field {fieldName} on {target.name}.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("m_value").stringValue = values[i].Value;
                element.FindPropertyRelative("m_id").intValue = GameplayTag.ComputeTagHash(values[i].Value);
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}

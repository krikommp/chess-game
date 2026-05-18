using MiniChess.Combat.Skills;
using MiniChess.GameplayTags;
using UnityEditor;
using UnityEngine;

namespace MiniChess.EditorTools
{
    public static class CreateBasicAttackAssets
    {
        private const string FunctionPath = "Assets/Data/EffectFunctions/modify_attribute.asset";
        private const string EffectPath = "Assets/Data/Effects/basic_attack_damage.asset";
        private const string AbilityPath = "Assets/Data/SkillAbilities/basic_attack_ability.asset";
        private const string DefinitionPath = "Assets/Data/Skills/basic_attack.asset";

        [MenuItem("MiniChess/Create Basic Attack Assets")]
        public static void Create()
        {
            EnsureDirectory("Assets/Data/EffectFunctions");
            EnsureDirectory("Assets/Data/Effects");
            EnsureDirectory("Assets/Data/SkillAbilities");
            EnsureDirectory("Assets/Data/Skills");

            var function = GetOrCreateModifyAttributeFunction();
            var effect = GetOrCreateDamageEffect(function);
            var ability = GetOrCreateBasicAttackAbility(effect);
            var definition = GetOrCreateBasicAttackDefinition(ability);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MiniChess] Basic attack SkillDefinition ready at {AssetDatabase.GetAssetPath(definition)}");
        }

        private static ModifyAttributeFunction GetOrCreateModifyAttributeFunction()
        {
            var function = AssetDatabase.LoadAssetAtPath<ModifyAttributeFunction>(FunctionPath);
            if (function == null)
            {
                function = ScriptableObject.CreateInstance<ModifyAttributeFunction>();
                AssetDatabase.CreateAsset(function, FunctionPath);
            }

            var so = new SerializedObject(function);
            so.FindProperty("m_amount").floatValue = 20f;
            so.FindProperty("m_attributeTag.m_value").stringValue = "Attribute.HP";
            so.FindProperty("m_attributeTag.m_id").intValue = GameplayTag.ComputeTagHash("Attribute.HP");
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(function);
            return function;
        }

        private static SkillEffect GetOrCreateDamageEffect(ModifyAttributeFunction function)
        {
            var effect = AssetDatabase.LoadAssetAtPath<SkillEffect>(EffectPath);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<SkillEffect>();
                effect.name = "basic_attack_damage";
                AssetDatabase.CreateAsset(effect, EffectPath);
            }

            var so = new SerializedObject(effect);
            so.FindProperty("m_function").objectReferenceValue = function;
            so.FindProperty("m_targetMapping").enumValueIndex = (int)ESkillEffectTarget.Target;
            var tagsProp = so.FindProperty("m_tags");
            tagsProp.arraySize = 1;
            var tag0 = tagsProp.GetArrayElementAtIndex(0);
            tag0.FindPropertyRelative("m_value").stringValue = "Effect.Damage.BasicAttack";
            tag0.FindPropertyRelative("m_id").intValue = GameplayTag.ComputeTagHash("Effect.Damage.BasicAttack");
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static SimpleTargetAbility GetOrCreateBasicAttackAbility(SkillEffect effect)
        {
            var ability = AssetDatabase.LoadAssetAtPath<SimpleTargetAbility>(AbilityPath);
            if (ability == null)
            {
                ability = ScriptableObject.CreateInstance<SimpleTargetAbility>();
                ability.name = "basic_attack_ability";
                AssetDatabase.CreateAsset(ability, AbilityPath);
            }

            var so = new SerializedObject(ability);
            var effectsProp = so.FindProperty("m_effects");
            effectsProp.arraySize = 1;
            effectsProp.GetArrayElementAtIndex(0).objectReferenceValue = effect;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static SkillDefinition GetOrCreateBasicAttackDefinition(SimpleTargetAbility ability)
        {
            var definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<SkillDefinition>();
                definition.name = "basic_attack";
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var so = new SerializedObject(definition);
            so.FindProperty("m_id").stringValue = "basic_attack";
            so.FindProperty("m_displayName").stringValue = "Basic Attack";
            so.FindProperty("m_description").stringValue = "A basic melee attack.";
            so.FindProperty("m_targetType").enumValueIndex = (int)ESkillTargetType.SingleEnemy;
            so.FindProperty("m_ability").objectReferenceValue = ability;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            var folderName = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}

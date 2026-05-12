using MiniChess.Combat.Skills;
using UnityEditor;
using UnityEngine;

public static class CreateBasicAttackAssets
{
    [MenuItem("MiniChess/Create Basic Attack Assets")]
    public static void Create()
    {
        EnsureDirectory("Assets/Data/EffectFunctions");

        // 1. Create ModifyAttributeFunction SO
        var funcPath = "Assets/Data/EffectFunctions/modify_attribute.asset";
        var func = AssetDatabase.LoadAssetAtPath<ModifyAttributeFunction>(funcPath);
        if (func == null)
        {
            func = ScriptableObject.CreateInstance<ModifyAttributeFunction>();
            var funcSo = new SerializedObject(func);
            funcSo.FindProperty("m_amount").floatValue = 20f;
            funcSo.FindProperty("m_attributeTag.m_value").stringValue = "Attribute.HP";
            funcSo.FindProperty("m_attributeTag.m_id").intValue = MiniChess.GameplayTags.GameplayTag.ComputeTagHash("Attribute.HP");
            funcSo.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(func, funcPath);
        }

        // 2. Create SkillEffect
        var fxPath = "Assets/Data/Effects/basic_attack_damage.asset";
        var effect = AssetDatabase.LoadAssetAtPath<SkillEffect>(fxPath);
        if (effect == null)
        {
            effect = ScriptableObject.CreateInstance<SkillEffect>();
            effect.name = "basic_attack_damage";
        }

        var fxSo = new SerializedObject(effect);
        fxSo.FindProperty("m_function").objectReferenceValue = func;
        var tagsProp = fxSo.FindProperty("m_tags");
        tagsProp.arraySize = 1;
        var tag0 = tagsProp.GetArrayElementAtIndex(0);
        tag0.FindPropertyRelative("m_value").stringValue = "Effect.Damage.BasicAttack";
        tag0.FindPropertyRelative("m_id").intValue = MiniChess.GameplayTags.GameplayTag.ComputeTagHash("Effect.Damage.BasicAttack");
        fxSo.ApplyModifiedProperties();

        if (!AssetDatabase.Contains(effect))
            AssetDatabase.CreateAsset(effect, fxPath);

        // 3. Create SimpleTargetAbility as the skill asset
        var skillPath = "Assets/Data/Skills/basic_attack.asset";
        var skill = AssetDatabase.LoadAssetAtPath<SimpleTargetAbility>(skillPath);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SimpleTargetAbility>();
            skill.name = "basic_attack";
        }

        var skillSo = new SerializedObject(skill);
        skillSo.FindProperty("m_id").stringValue = "basic_attack";
        skillSo.FindProperty("m_displayName").stringValue = "Basic Attack";
        skillSo.FindProperty("m_description").stringValue = "A basic melee attack.";
        var effectsProp = skillSo.FindProperty("m_effects");
        effectsProp.arraySize = 1;
        effectsProp.GetArrayElementAtIndex(0).objectReferenceValue = effect;
        skillSo.ApplyModifiedProperties();

        if (!AssetDatabase.Contains(skill))
            AssetDatabase.CreateAsset(skill, skillPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MiniChess] Created ModifyAttributeFunction + SkillEffect + SimpleTargetAbility assets");
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

using MiniChess.Combat.Skills;
using UnityEditor;
using UnityEngine;

public static class CreateBasicAttackAssets
{
    [MenuItem("MiniChess/Create Basic Attack Assets")]
    public static void Create()
    {
        // 1. Create DamageEffect
        var damageFx = ScriptableObject.CreateInstance<EffectDefinition>();
        damageFx.name = "basic_attack_damage";

        var fxSo = new SerializedObject(damageFx);
        fxSo.FindProperty("m_function").enumValueIndex = (int)EEffectFunction.ModifyAttribute;
        fxSo.FindProperty("m_amount").floatValue = 20f;
        var attrTagProp = fxSo.FindProperty("m_attributeTag");
        attrTagProp.FindPropertyRelative("m_value").stringValue = "Attribute.HP";
        attrTagProp.FindPropertyRelative("m_id").intValue = MiniChess.GameplayTags.GameplayTag.ComputeTagHash("Attribute.HP");
        var tagsProp = fxSo.FindProperty("m_tags");
        tagsProp.arraySize = 1;
        var tag0 = tagsProp.GetArrayElementAtIndex(0);
        tag0.FindPropertyRelative("m_value").stringValue = "Effect.Damage.BasicAttack";
        tag0.FindPropertyRelative("m_id").intValue = MiniChess.GameplayTags.GameplayTag.ComputeTagHash("Effect.Damage.BasicAttack");
        fxSo.ApplyModifiedProperties();

        string fxPath = "Assets/Data/Effects/basic_attack_damage.asset";
        AssetDatabase.CreateAsset(damageFx, fxPath);

        // 2. Create SimpleTargetAbility as the skill asset
        var skill = ScriptableObject.CreateInstance<SimpleTargetAbility>();
        skill.name = "basic_attack";

        var skillSo = new SerializedObject(skill);
        skillSo.FindProperty("m_id").stringValue = "basic_attack";
        skillSo.FindProperty("m_displayName").stringValue = "Basic Attack";
        skillSo.FindProperty("m_description").stringValue = "A basic melee attack.";
        // Assign effect
        var effectsProp = skillSo.FindProperty("m_effects");
        effectsProp.arraySize = 1;
        effectsProp.GetArrayElementAtIndex(0).objectReferenceValue = damageFx;
        skillSo.ApplyModifiedProperties();

        string skillPath = "Assets/Data/Skills/basic_attack.asset";
        AssetDatabase.CreateAsset(skill, skillPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MiniChess] Created basic_attack_damage.asset and basic_attack.asset (EffectDefinition + SimpleTargetAbility)");
    }
}

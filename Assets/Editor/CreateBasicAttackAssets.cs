using MiniChess.Combat.Skills;
using UnityEditor;
using UnityEngine;

public static class CreateBasicAttackAssets
{
    [MenuItem("MiniChess/Create Basic Attack Assets")]
    public static void Create()
    {
        // 1. Create DamageEffect
        var damageFx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        damageFx.name = "basic_attack_damage";

        var fxSo = new SerializedObject(damageFx);
        fxSo.FindProperty("m_amount").intValue = 20;
        // Add tag
        var tagsProp = fxSo.FindProperty("m_tags");
        tagsProp.arraySize = 1;
        tagsProp.GetArrayElementAtIndex(0).FindPropertyRelative("m_value").stringValue = "Effect.Damage.BasicAttack";
        fxSo.ApplyModifiedProperties();

        string fxPath = "Assets/Data/Effects/basic_attack_damage.asset";
        AssetDatabase.CreateAsset(damageFx, fxPath);

        // 2. Create SkillDefinition
        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        skill.name = "basic_attack";

        var skillSo = new SerializedObject(skill);
        skillSo.FindProperty("m_id").stringValue = "basic_attack";
        skillSo.FindProperty("m_displayName").stringValue = "Basic Attack";
        skillSo.FindProperty("m_description").stringValue = "A basic melee attack.";
        skillSo.FindProperty("m_apCost").intValue = 1;
        skillSo.FindProperty("m_cooldown").intValue = 0;
        skillSo.FindProperty("m_range").floatValue = 1.5f;
        skillSo.FindProperty("m_targetType").enumValueIndex = (int)ESkillTargetType.SingleEnemy;
        skillSo.FindProperty("m_aiBaseWeight").floatValue = 10f;
        // Add skill tag
        var skillTagsProp = skillSo.FindProperty("m_skillTags");
        skillTagsProp.arraySize = 1;
        skillTagsProp.GetArrayElementAtIndex(0).FindPropertyRelative("m_value").stringValue = "Skill.Attack.Melee";
        // Add AI tag
        var aiTagsProp = skillSo.FindProperty("m_aiTags");
        aiTagsProp.arraySize = 1;
        aiTagsProp.GetArrayElementAtIndex(0).FindPropertyRelative("m_value").stringValue = "AI.Skill.Damage";
        // Assign effect
        var effectsProp = skillSo.FindProperty("m_effects");
        effectsProp.arraySize = 1;
        effectsProp.GetArrayElementAtIndex(0).objectReferenceValue = damageFx;
        skillSo.ApplyModifiedProperties();

        string skillPath = "Assets/Data/Skills/basic_attack.asset";
        AssetDatabase.CreateAsset(skill, skillPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MiniChess] Created basic_attack_damage.asset and basic_attack.asset");
    }
}

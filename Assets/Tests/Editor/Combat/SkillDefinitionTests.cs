using MiniChess.Combat.Skills;
using MiniChess.GameplayTags;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class SkillDefinitionTests
{
    [Test]
    public void TargetType_HasAllValues()
    {
        Assert.AreEqual(0, (int)ESkillTargetType.Self);
        Assert.AreEqual(1, (int)ESkillTargetType.SingleEnemy);
        Assert.AreEqual(2, (int)ESkillTargetType.SingleAlly);
        Assert.AreEqual(3, (int)ESkillTargetType.GroundPoint);
        Assert.AreEqual(4, (int)ESkillTargetType.Area);
    }

    // ── EffectDefinition (sealed, data-only) ──────────────────────

    [Test]
    public void EffectDefinition_Defaults()
    {
        var fx = ScriptableObject.CreateInstance<EffectDefinition>();
        Assert.AreEqual(EEffectFunction.SpendAP, fx.Function); // first enum value
        Assert.AreEqual(EEffectDuration.Instant, fx.Duration);
        Assert.AreEqual(0, fx.Tags.Length);
        Assert.IsFalse(fx.HasAnyTag());
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void EffectDefinition_HasAnyTag_TrueWhenTagPresent()
    {
        var fx = ScriptableObject.CreateInstance<EffectDefinition>();
        SetTagsOnEffect(fx, "Effect.Damage.Physical");
        Assert.IsTrue(fx.HasAnyTag());
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void EffectDefinition_HasAnyTag_FalseWhenTagsInvalid()
    {
        var fx = ScriptableObject.CreateInstance<EffectDefinition>();
        SetTagsOnEffect(fx, "");
        Assert.IsFalse(fx.HasAnyTag());
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void EffectDefinition_SetFunction()
    {
        var fx = ScriptableObject.CreateInstance<EffectDefinition>();
        var so = new SerializedObject(fx);
        so.FindProperty("m_function").enumValueIndex = (int)EEffectFunction.ModifyAttribute;
        so.ApplyModifiedProperties();
        Assert.AreEqual(EEffectFunction.ModifyAttribute, fx.Function);
        Object.DestroyImmediate(fx);
    }

    // ── SkillDefinition ──────────────────────────────────────────

    [Test]
    public void SkillDefinition_Defaults()
    {
        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        Assert.AreEqual(string.Empty, skill.Id);
        Assert.AreEqual(string.Empty, skill.DisplayName);
        Assert.AreEqual(1.5f, skill.Range, 0.001f);
        Assert.AreEqual(ESkillTargetType.SingleEnemy, skill.TargetType);
        Assert.AreEqual(0, skill.Effects.Length);
        Assert.AreEqual(0, skill.SkillTags.Length);
        Assert.AreEqual(0, skill.AiTags.Length);
        Assert.AreEqual(10f, skill.AiBaseWeight, 0.001f);
        Assert.IsNull(skill.Ability); // No default ability fallback
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void SkillDefinition_SetFields()
    {
        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        var so = new SerializedObject(skill);
        so.FindProperty("m_id").stringValue = "basic_attack";
        so.FindProperty("m_displayName").stringValue = "Basic Attack";
        so.FindProperty("m_range").floatValue = 3f;
        so.FindProperty("m_targetType").enumValueIndex = (int)ESkillTargetType.Self;
        so.FindProperty("m_aiBaseWeight").floatValue = 25f;
        so.ApplyModifiedProperties();

        Assert.AreEqual("basic_attack", skill.Id);
        Assert.AreEqual("Basic Attack", skill.DisplayName);
        Assert.AreEqual(3f, skill.Range, 0.001f);
        Assert.AreEqual(ESkillTargetType.Self, skill.TargetType);
        Assert.AreEqual(25f, skill.AiBaseWeight, 0.001f);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void SkillDefinition_HasEffectTag_TrueWhenEffectHasTag()
    {
        var fx = ScriptableObject.CreateInstance<EffectDefinition>();
        SetTagsOnEffect(fx, "Effect.Damage.BasicAttack");

        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        SetEffectsOnSkill(skill, fx);

        Assert.IsTrue(skill.HasEffectTag(new GameplayTag("Effect.Damage.BasicAttack")));
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void SkillDefinition_HasEffectTag_FalseWhenNoMatch()
    {
        var fx = ScriptableObject.CreateInstance<EffectDefinition>();
        SetTagsOnEffect(fx, "Effect.Damage.Physical");

        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        SetEffectsOnSkill(skill, fx);

        Assert.IsFalse(skill.HasEffectTag(new GameplayTag("Effect.Heal.Direct")));
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void SkillDefinition_HasEffectTag_FalseWhenNoEffects()
    {
        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        Assert.IsFalse(skill.HasEffectTag(new GameplayTag("Effect.Damage.Physical")));
        Object.DestroyImmediate(skill);
    }

    // ── AISkillTag ────────────────────────────────────────────────

    [Test]
    public void AISkillTag_AllConstants_AreValid()
    {
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.k_Damage.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.k_Heal.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.k_Buff.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.k_Debuff.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.k_Mobility.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.k_Control.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.k_Protect.Value));
    }

    [Test]
    public void AISkillTag_Damage_HasCorrectValue()
    {
        Assert.AreEqual("AI.Skill.Damage", AISkillTag.k_Damage.Value);
        Assert.AreEqual("AI.Skill.Heal", AISkillTag.k_Heal.Value);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static void SetTagsOnEffect(EffectDefinition fx, string tagValue)
    {
        var so = new SerializedObject(fx);
        var prop = so.FindProperty("m_tags");
        prop.arraySize = 1;
        prop.GetArrayElementAtIndex(0).FindPropertyRelative("m_value").stringValue = tagValue;
        so.ApplyModifiedProperties();
    }

    private static void SetEffectsOnSkill(SkillDefinition skill, EffectDefinition fx)
    {
        var so = new SerializedObject(skill);
        var prop = so.FindProperty("m_effects");
        prop.arraySize = 1;
        prop.GetArrayElementAtIndex(0).objectReferenceValue = fx;
        so.ApplyModifiedProperties();
    }
}

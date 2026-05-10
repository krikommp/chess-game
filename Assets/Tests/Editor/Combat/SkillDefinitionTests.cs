using MiniChess.Combat.Skills;
using MiniChess.GameplayTags;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class SkillDefinitionTests
{
    // ── SkillTargetType ────────────────────────────────────────

    [Test]
    public void TargetType_HasAllValues()
    {
        Assert.AreEqual(0, (int)SkillTargetType.Self);
        Assert.AreEqual(1, (int)SkillTargetType.SingleEnemy);
        Assert.AreEqual(2, (int)SkillTargetType.SingleAlly);
        Assert.AreEqual(3, (int)SkillTargetType.GroundPoint);
        Assert.AreEqual(4, (int)SkillTargetType.Area);
    }

    // ── EffectDefinition ───────────────────────────────────────

    [Test]
    public void EffectDefinition_DefaultTags_Empty()
    {
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        Assert.AreEqual(0, fx.Tags.Length);
        Assert.IsFalse(fx.HasAnyTag());
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void EffectDefinition_HasAnyTag_TrueWhenTagPresent()
    {
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        SetTagsOnEffect(fx, "Effect.Damage.Physical");
        Assert.IsTrue(fx.HasAnyTag());
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void EffectDefinition_HasAnyTag_FalseWhenTagsInvalid()
    {
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        SetTagsOnEffect(fx, "");
        Assert.IsFalse(fx.HasAnyTag());
        Object.DestroyImmediate(fx);
    }

    // ── DamageEffectDefinition ─────────────────────────────────

    [Test]
    public void DamageEffect_DefaultAmount_Is20()
    {
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        Assert.AreEqual(20, fx.Amount);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void DamageEffect_SetAmount()
    {
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        var so = new SerializedObject(fx);
        so.FindProperty("_amount").intValue = 50;
        so.ApplyModifiedProperties();
        Assert.AreEqual(50, fx.Amount);
        Object.DestroyImmediate(fx);
    }

    // ── HealEffectDefinition ───────────────────────────────────

    [Test]
    public void HealEffect_DefaultAmount_Is10()
    {
        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        Assert.AreEqual(10, fx.Amount);
        Object.DestroyImmediate(fx);
    }

    // ── AddStatusEffectDefinition ──────────────────────────────

    [Test]
    public void AddStatusEffect_DefaultValues()
    {
        var fx = ScriptableObject.CreateInstance<AddStatusEffectDefinition>();
        Assert.AreEqual(string.Empty, fx.StatusId);
        Assert.AreEqual(1, fx.DurationTurns);
        Object.DestroyImmediate(fx);
    }

    // ── SkillDefinition ────────────────────────────────────────

    [Test]
    public void SkillDefinition_Defaults()
    {
        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        Assert.AreEqual(string.Empty, skill.Id);
        Assert.AreEqual(string.Empty, skill.DisplayName);
        Assert.AreEqual(1, skill.ApCost);
        Assert.AreEqual(0, skill.Cooldown);
        Assert.AreEqual(1.5f, skill.Range, 0.001f);
        Assert.AreEqual(SkillTargetType.SingleEnemy, skill.TargetType);
        Assert.AreEqual(0, skill.Effects.Length);
        Assert.AreEqual(0, skill.SkillTags.Length);
        Assert.AreEqual(0, skill.AiTags.Length);
        Assert.AreEqual(10f, skill.AiBaseWeight, 0.001f);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void SkillDefinition_SetFields()
    {
        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        var so = new SerializedObject(skill);
        so.FindProperty("_id").stringValue = "basic_attack";
        so.FindProperty("_displayName").stringValue = "Basic Attack";
        so.FindProperty("_apCost").intValue = 2;
        so.FindProperty("_cooldown").intValue = 1;
        so.FindProperty("_range").floatValue = 3f;
        so.FindProperty("_targetType").enumValueIndex = (int)SkillTargetType.Self;
        so.FindProperty("_aiBaseWeight").floatValue = 25f;
        so.ApplyModifiedProperties();

        Assert.AreEqual("basic_attack", skill.Id);
        Assert.AreEqual("Basic Attack", skill.DisplayName);
        Assert.AreEqual(2, skill.ApCost);
        Assert.AreEqual(1, skill.Cooldown);
        Assert.AreEqual(3f, skill.Range, 0.001f);
        Assert.AreEqual(SkillTargetType.Self, skill.TargetType);
        Assert.AreEqual(25f, skill.AiBaseWeight, 0.001f);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void SkillDefinition_HasEffectTag_TrueWhenEffectHasTag()
    {
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
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
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
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

    // ── AISkillTag ─────────────────────────────────────────────

    [Test]
    public void AISkillTag_AllConstants_AreValid()
    {
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.Damage.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.Heal.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.Buff.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.Debuff.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.Mobility.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.Control.Value));
        Assert.IsTrue(GameplayTag.IsValid(AISkillTag.Protect.Value));
    }

    [Test]
    public void AISkillTag_Damage_HasCorrectValue()
    {
        Assert.AreEqual("AI.Skill.Damage", AISkillTag.Damage.Value);
        Assert.AreEqual("AI.Skill.Heal", AISkillTag.Heal.Value);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static void SetTagsOnEffect(EffectDefinition fx, string tagValue)
    {
        var so = new SerializedObject(fx);
        var prop = so.FindProperty("_tags");
        prop.arraySize = 1;
        prop.GetArrayElementAtIndex(0).FindPropertyRelative("_value").stringValue = tagValue;
        so.ApplyModifiedProperties();
    }

    private static void SetEffectsOnSkill(SkillDefinition skill, EffectDefinition fx)
    {
        var so = new SerializedObject(skill);
        var prop = so.FindProperty("_effects");
        prop.arraySize = 1;
        prop.GetArrayElementAtIndex(0).objectReferenceValue = fx;
        so.ApplyModifiedProperties();
    }
}


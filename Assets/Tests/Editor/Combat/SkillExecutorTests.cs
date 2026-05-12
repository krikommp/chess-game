using MiniChess.Combat;
using MiniChess.Combat.Skills;
using MiniChess.GameplayTags;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[TestFixture]
public class SkillExecutorTests
{
    private GameObject m_casterObj;
    private GameObject m_targetObj;
    private TestCombatUnit m_casterUnit;
    private TestCombatUnit m_targetUnit;
    private SkillExecutor m_casterExecutor;
    private SkillExecutor m_targetExecutor;

    [SetUp]
    public void SetUp()
    {
        m_casterObj = new GameObject("TestCaster");
        m_targetObj = new GameObject("TestTarget");

        m_casterUnit = m_casterObj.AddComponent<TestCombatUnit>();
        m_targetUnit = m_targetObj.AddComponent<TestCombatUnit>();

        m_casterUnit.SetAlive(true);
        m_targetUnit.SetAlive(true);
        m_casterUnit.SetCurrentAP(6);
        m_targetUnit.SetFaction(EFaction.Enemy);

        m_casterExecutor = m_casterObj.AddComponent<SkillExecutor>();
        m_targetExecutor = m_targetObj.AddComponent<SkillExecutor>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(m_casterObj);
        Object.DestroyImmediate(m_targetObj);
    }

    // ── CanCast ──────────────────────────────────────────────────

    [Test]
    public void CanCast_ReturnsSuccess_WhenAllChecksPass()
    {
        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("test_skill", ability);
        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsTrue(result.IsSuccess);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void CanCast_Fails_WhenSkillIsNull()
    {
        var result = m_casterExecutor.CanCast(null, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetInvalid, result.Failure);
    }

    [Test]
    public void CanCast_Fails_WhenCasterDead()
    {
        m_casterUnit.SetAlive(false);
        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("test_skill", ability);
        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.CasterDead, result.Failure);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void CanCast_Fails_WhenSkillHasNoAbility()
    {
        // All castable skills must have an explicit Ability
        var skill = CreateSkillNoAbility("test_skill");
        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.EffectApplicationFailed, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_Succeeds_ForSelfTarget_WhenTargetNull()
    {
        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("self_skill", ability, targetType: ESkillTargetType.Self);
        var result = m_casterExecutor.CanCast(skill, null);
        Assert.IsTrue(result.IsSuccess);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void CanCast_Fails_WhenTargetDead()
    {
        m_targetUnit.SetAlive(false);
        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("test_skill", ability, targetType: ESkillTargetType.SingleEnemy);
        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetDead, result.Failure);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    // ── Execute ──────────────────────────────────────────────────

    [Test]
    public void Execute_DelegatesToAbility()
    {
        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("test_skill", ability);
        var result = m_casterExecutor.Execute(skill, m_targetObj);
        Assert.IsTrue(result.IsSuccess);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void Execute_ReturnsFailure_WhenCanCastFails()
    {
        // Skill with no ability fails CanCast
        var skill = CreateSkillNoAbility("test_skill");
        var result = m_casterExecutor.Execute(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Object.DestroyImmediate(skill);
    }

    // ── Tag conditions ───────────────────────────────────────────

    [Test]
    public void CanCast_Fails_WhenCasterLacksRequiredTag()
    {
        m_targetUnit.SetFaction(EFaction.Enemy);
        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("tagged_skill", ability);
        SetSkillTagConditions(skill, requiredCaster: new[] { "Element.Fire" });

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TagConditionFailed, result.Failure);

        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void CanCast_Fails_WhenCasterHasBlockedTag()
    {
        m_targetUnit.SetFaction(EFaction.Enemy);
        var tagComp = m_casterObj.AddComponent<GameplayTagComponent>();
        tagComp.AddTag("State.Silenced");

        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("tagged_skill", ability);
        SetSkillTagConditions(skill, blockedCaster: new[] { "State.Silenced" });

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TagConditionFailed, result.Failure);

        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void CanCast_Fails_WhenTargetHasBlockedTag()
    {
        m_targetUnit.SetFaction(EFaction.Enemy);
        var tagComp = m_targetObj.AddComponent<GameplayTagComponent>();
        tagComp.AddTag("State.Immune.Physical");

        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("tagged_skill", ability);
        SetSkillTagConditions(skill, blockedTarget: new[] { "State.Immune.Physical" });

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TagConditionFailed, result.Failure);

        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void CanCast_Succeeds_WhenTagConditionsMet()
    {
        m_targetUnit.SetFaction(EFaction.Enemy);
        var casterTagComp = m_casterObj.AddComponent<GameplayTagComponent>();
        casterTagComp.AddTag("Element.Fire");
        var targetTagComp = m_targetObj.AddComponent<GameplayTagComponent>();
        targetTagComp.AddTag("Target.Unit");

        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("tagged_skill", ability);
        SetSkillTagConditions(skill,
            requiredCaster: new[] { "Element.Fire" },
            requiredTarget: new[] { "Target.Unit" },
            blockedTarget: new[] { "State.Immune.Physical" });

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsTrue(result.IsSuccess);

        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void CanCast_FactionTagAutoSync_AllowsFactionBasedTargeting()
    {
        m_targetUnit.SetFaction(EFaction.Enemy);
        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("faction_attack", ability, targetType: ESkillTargetType.SingleEnemy);
        SetSkillTagConditions(skill, requiredTarget: new[] { "Faction.Enemy" });

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsTrue(result.IsSuccess);

        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    // ── GroundPoint skills ───────────────────────────────────────

    [Test]
    public void CanExecute_WithGroundPoint_Fails_WhenSkillIsNull()
    {
        var ctx = SkillExecutionContext.ForGroundPoint(m_casterExecutor, null, Vector3.zero, null);
        var result = m_casterExecutor.CanExecute(ctx);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetInvalid, result.Failure);
    }

    [Test]
    public void CanExecute_WithGroundPoint_Fails_WhenCasterDead()
    {
        m_casterUnit.SetAlive(false);
        var ability = ScriptableObject.CreateInstance<GroundMoveAbility>();
        var skill = CreateSkill("move_skill", ability, targetType: ESkillTargetType.GroundPoint);
        var ctx = SkillExecutionContext.ForGroundPoint(m_casterExecutor, skill, Vector3.zero, null);
        var result = m_casterExecutor.CanExecute(ctx);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.CasterDead, result.Failure);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void CanExecute_WithGroundPoint_Succeeds_ForValidSkill()
    {
        var ability = ScriptableObject.CreateInstance<GroundMoveAbility>();
        var skill = CreateSkill("move_skill", ability, targetType: ESkillTargetType.GroundPoint);
        var ctx = SkillExecutionContext.ForGroundPoint(m_casterExecutor, skill, Vector3.zero, null);
        // GroundPoint skips most target validation; succeeds at CanExecute level
        var result = m_casterExecutor.CanExecute(ctx);
        Assert.IsTrue(result.IsSuccess);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void CanExecute_WithGroundPoint_Fails_WhenCasterTagBlocked()
    {
        var tagComp = m_casterObj.AddComponent<GameplayTagComponent>();
        tagComp.AddTag("State.Rooted");

        var ability = ScriptableObject.CreateInstance<GroundMoveAbility>();
        var skill = CreateSkill("move_skill", ability, targetType: ESkillTargetType.GroundPoint);
        SetSkillTagConditions(skill, blockedCaster: new[] { "State.Rooted" });

        var ctx = SkillExecutionContext.ForGroundPoint(m_casterExecutor, skill, Vector3.zero, null);
        var result = m_casterExecutor.CanExecute(ctx);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TagConditionFailed, result.Failure);

        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void Execute_WithGroundPoint_Fails_WhenNoCompletePath()
    {
        var ability = ScriptableObject.CreateInstance<GroundMoveAbility>();
        var skill = CreateSkill("move_skill", ability, targetType: ESkillTargetType.GroundPoint);
        // null path → GroundMoveAbility.Execute will fail
        var ctx = SkillExecutionContext.ForGroundPoint(m_casterExecutor, skill, Vector3.zero, null);
        var result = m_casterExecutor.Execute(ctx);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetInvalid, result.Failure);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    // ── Default Values ───────────────────────────────────────────

    [Test]
    public void SkillExecutor_DefaultCapabilities_IsDamageable()
    {
        var obj = new GameObject("DefaultExecutor");
        var executor = obj.AddComponent<SkillExecutor>();
        Assert.AreEqual(ETargetCapability.Damageable, executor.Capabilities);
        Object.DestroyImmediate(obj);
    }

    [Test]
    public void SkillExecutor_DefaultSkills_Empty()
    {
        Assert.AreEqual(0, m_casterExecutor.AvailableSkills.Length);
    }

    [Test]
    public void SkillExecutor_DefaultCooldown_Zero()
    {
        Assert.AreEqual(0, m_casterExecutor.GetCooldownRemaining("any_skill"));
    }

    [Test]
    public void SkillExecutor_ActivateSkill_SetsActiveSkill()
    {
        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("active_test", ability);
        m_casterExecutor.SetSkills(new[] { skill });
        Assert.IsTrue(m_casterExecutor.ActivateSkill(skill));
        Assert.AreEqual(skill, m_casterExecutor.ActiveSkill);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    [Test]
    public void SkillExecutor_FindSkill_ById()
    {
        var ability = ScriptableObject.CreateInstance<TestSimpleAbility>();
        var skill = CreateSkill("find_me", ability);
        m_casterExecutor.SetSkills(new[] { skill });
        var found = m_casterExecutor.FindSkill("find_me");
        Assert.AreEqual(skill, found);
        var notFound = m_casterExecutor.FindSkill("nonexistent");
        Assert.IsNull(notFound);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(ability);
    }

    // ── Cooldowns ────────────────────────────────────────────────

    [Test]
    public void SetCooldown_RecordsCooldown()
    {
        m_casterExecutor.SetCooldown("test_cd", 3);
        Assert.AreEqual(3, m_casterExecutor.GetCooldownRemaining("test_cd"));
    }

    [Test]
    public void AdvanceCooldowns_ReducesCooldownByOne()
    {
        m_casterExecutor.SetCooldown("cd_skill", 2);
        m_casterExecutor.AdvanceCooldowns();
        Assert.AreEqual(1, m_casterExecutor.GetCooldownRemaining("cd_skill"));
    }

    [Test]
    public void AdvanceCooldowns_RemovesExpiredCooldown()
    {
        m_casterExecutor.SetCooldown("cd_skill", 1);
        m_casterExecutor.AdvanceCooldowns();
        Assert.AreEqual(0, m_casterExecutor.GetCooldownRemaining("cd_skill"));
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static SkillDefinition CreateSkill(string id, SkillAbility ability,
        ESkillTargetType targetType = ESkillTargetType.SingleEnemy)
    {
        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        var so = new SerializedObject(skill);
        so.FindProperty("m_id").stringValue = id;
        so.FindProperty("m_targetType").enumValueIndex = (int)targetType;
        so.FindProperty("m_ability").objectReferenceValue = ability;
        so.ApplyModifiedProperties();
        return skill;
    }

    private static SkillDefinition CreateSkillNoAbility(string id,
        ESkillTargetType targetType = ESkillTargetType.SingleEnemy)
    {
        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        var so = new SerializedObject(skill);
        so.FindProperty("m_id").stringValue = id;
        so.FindProperty("m_targetType").enumValueIndex = (int)targetType;
        so.ApplyModifiedProperties();
        return skill;
    }

    private static void SetSkillTagConditions(
        SkillDefinition skill,
        string[] requiredCaster = null,
        string[] blockedCaster = null,
        string[] requiredTarget = null,
        string[] blockedTarget = null)
    {
        var so = new SerializedObject(skill);
        SetTagRefArray(so, "m_requiredCasterTags", requiredCaster);
        SetTagRefArray(so, "m_blockedCasterTags", blockedCaster);
        SetTagRefArray(so, "m_requiredTargetTags", requiredTarget);
        SetTagRefArray(so, "m_blockedTargetTags", blockedTarget);
        so.ApplyModifiedProperties();
    }

    private static void SetTagRefArray(SerializedObject so, string propertyName, string[] values)
    {
        if (values == null) return;
        var prop = so.FindProperty(propertyName);
        if (prop == null) return;
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            var elem = prop.GetArrayElementAtIndex(i);
            var valueProp = elem.FindPropertyRelative("m_value");
            if (valueProp != null)
                valueProp.stringValue = values[i];
        }
    }

    // ── Test Ability (SimpleTargetAbility-style, always succeeds) ──

    private class TestSimpleAbility : SkillAbility
    {
        public override SkillCastResult Execute(SkillExecutionContext context)
        {
            return SkillCastResult.Success();
        }
    }

    // ── Test AttributeSet implementation ─────────────────────────

    private class TestCombatUnit : AttributeSet
    {
        public int CurrentAP => (int)Get(WellKnownAttributeTags.AP);
        public int MaxHP => (int)GetMax(WellKnownAttributeTags.HP);
        public int CurrentHP => (int)Get(WellKnownAttributeTags.HP);

        private void Awake()
        {
            Testing_AddAttribute(WellKnownAttributeTags.HP, 100f, 100f);
            Testing_AddAttribute(WellKnownAttributeTags.AP, 6f, 6f);
            Testing_AddAttribute(WellKnownAttributeTags.Initiative, 10f, 0f);
            Testing_AddAttribute(WellKnownAttributeTags.MoveSpeed, 2f, 0f);
        }

        public void SetAlive(bool alive)
        {
            if (alive)
            {
                Set(WellKnownAttributeTags.HP, Mathf.Max(1f, GetMax(WellKnownAttributeTags.HP)));
                return;
            }
            Set(WellKnownAttributeTags.HP, 0f);
        }

        public void SetCurrentAP(int ap) => Set(WellKnownAttributeTags.AP, ap);
        public void SetMaxHP(int hp) => SetMax(WellKnownAttributeTags.HP, hp);
        public void SetCurrentHP(int hp) => Set(WellKnownAttributeTags.HP, hp);
        public void SetFaction(EFaction faction) => OverrideFactionForTesting(faction);
    }
}

internal static class SkillExecutorExtensions
{
    public static void SetCapabilities(this SkillExecutor executor, ETargetCapability capabilities)
    {
        var so = new SerializedObject(executor);
        so.FindProperty("m_capabilities").intValue = (int)capabilities;
        so.ApplyModifiedProperties();
    }
}

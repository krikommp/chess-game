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
        m_targetUnit.SetFaction(EFaction.Enemy); // default: most tests target enemies

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
        var skill = CreateSkill("test_skill", apCost: 2);
        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsTrue(result.IsSuccess);
        Object.DestroyImmediate(skill);
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
        var skill = CreateSkill("test_skill");
        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.CasterDead, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_Fails_WhenInsufficientAp()
    {
        m_casterUnit.SetCurrentAP(0);
        var skill = CreateSkill("test_skill", apCost: 1);
        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.InsufficientAp, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_Fails_WhenOnCooldown()
    {
        var skill = CreateSkill("test_skill", cooldown: 2, apCost: 1);
        // Execute once to trigger cooldown
        m_casterExecutor.Execute(skill, m_targetObj);
        // Now on cooldown
        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.OnCooldown, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_Fails_WhenTargetDead()
    {
        m_targetUnit.SetAlive(false);
        var skill = CreateSkill("test_skill", targetType: ESkillTargetType.SingleEnemy);
        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetDead, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_Succeeds_ForSelfTarget_WhenTargetNull()
    {
        var skill = CreateSkill("self_skill", targetType: ESkillTargetType.Self);
        var result = m_casterExecutor.CanCast(skill, null);
        Assert.IsTrue(result.IsSuccess);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_Fails_WhenTargetCapabilityBlocked()
    {
        m_targetExecutor.SetCapabilities(ETargetCapability.Healable);
        var skill = CreateSkill("test_skill");
        // Assign a damage effect to the skill
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        SetEffectAmount(fx, 20);
        SetEffectsOnSkill(skill, fx);

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetCapabilityBlocked, result.Failure);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void CanCast_Fails_WhenTargetHasNoSkillExecutor()
    {
        // Non-Self targets MUST have SkillExecutor (CR-0003).
        var objNoExecutor = new GameObject("NoExecutorTarget");
        objNoExecutor.AddComponent<TestCombatUnit>().SetAlive(true);

        var skill = CreateSkill("test_skill");
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        SetEffectAmount(fx, 20);
        SetEffectsOnSkill(skill, fx);

        var result = m_casterExecutor.CanCast(skill, objNoExecutor);
        Assert.IsFalse(result.IsSuccess, "Non-Self target without SkillExecutor should fail.");
        Assert.AreEqual(ESkillCastFailure.TargetCapabilityBlocked, result.Failure);

        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
        Object.DestroyImmediate(objNoExecutor);
    }

    // ── Execute ──────────────────────────────────────────────────

    [Test]
    public void Execute_DeductsAp()
    {
        m_casterUnit.SetCurrentAP(6);
        var skill = CreateSkill("test_skill", apCost: 2);
        m_casterExecutor.Execute(skill, m_targetObj);
        Assert.AreEqual(4, m_casterUnit.CurrentAP);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void Execute_AppliesDamageEffect()
    {
        m_targetUnit.SetMaxHP(100);
        m_targetUnit.SetCurrentHP(100);

        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        SetEffectAmount(fx, 25);
        var skill = CreateSkill("test_skill", apCost: 1);
        SetEffectsOnSkill(skill, fx);

        m_casterExecutor.Execute(skill, m_targetObj);

        Assert.AreEqual(75, m_targetUnit.CurrentHP);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void Execute_AppliesHealEffect()
    {
        m_targetUnit.SetFaction(EFaction.Player); // heal targets are allies
        m_targetUnit.SetMaxHP(100);
        m_targetUnit.SetCurrentHP(50);
        m_targetExecutor.SetCapabilities(ETargetCapability.Healable);

        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        SetEffectAmount(fx, 20);
        var skill = CreateSkill("test_skill", apCost: 1, targetType: ESkillTargetType.SingleAlly);
        SetEffectsOnSkill(skill, fx);

        m_casterExecutor.Execute(skill, m_targetObj);

        Assert.AreEqual(70, m_targetUnit.CurrentHP);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void Execute_HealDoesNotExceedMaxHP()
    {
        m_targetUnit.SetFaction(EFaction.Player); // heal targets are allies
        m_targetUnit.SetMaxHP(100);
        m_targetUnit.SetCurrentHP(95);
        m_targetExecutor.SetCapabilities(ETargetCapability.Healable);

        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        SetEffectAmount(fx, 20);
        var skill = CreateSkill("test_skill", apCost: 1, targetType: ESkillTargetType.SingleAlly);
        SetEffectsOnSkill(skill, fx);

        m_casterExecutor.Execute(skill, m_targetObj);

        Assert.AreEqual(100, m_targetUnit.CurrentHP);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void Execute_ReturnsFailure_WhenCanCastFails()
    {
        m_casterUnit.SetCurrentAP(0);
        var skill = CreateSkill("test_skill", apCost: 1);
        var result = m_casterExecutor.Execute(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.InsufficientAp, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void Execute_DoesNotDamageDeadTarget()
    {
        m_targetUnit.SetAlive(false);
        m_targetUnit.SetCurrentHP(0);

        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        SetEffectAmount(fx, 25);
        var skill = CreateSkill("test_skill", apCost: 1);
        SetEffectsOnSkill(skill, fx);

        var result = m_casterExecutor.Execute(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        // HP should remain 0 (effect Apply checks IsAlive)
        Assert.AreEqual(0, m_targetUnit.CurrentHP);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    // ── Cooldown ─────────────────────────────────────────────────

    [Test]
    public void Execute_RecordsCooldown()
    {
        var skill = CreateSkill("cooldown_skill", cooldown: 3, apCost: 1);
        m_casterExecutor.Execute(skill, m_targetObj);
        Assert.AreEqual(3, m_casterExecutor.GetCooldownRemaining("cooldown_skill"));
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void Execute_NoCooldown_WhenCooldownIsZero()
    {
        var skill = CreateSkill("no_cd_skill", cooldown: 0, apCost: 1);
        m_casterExecutor.Execute(skill, m_targetObj);
        Assert.AreEqual(0, m_casterExecutor.GetCooldownRemaining("no_cd_skill"));
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void AdvanceCooldowns_ReducesCooldownByOne()
    {
        var skill = CreateSkill("cd_skill", cooldown: 2, apCost: 1);
        m_casterExecutor.Execute(skill, m_targetObj);

        m_casterExecutor.AdvanceCooldowns();
        Assert.AreEqual(1, m_casterExecutor.GetCooldownRemaining("cd_skill"));
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void AdvanceCooldowns_RemovesExpiredCooldown()
    {
        var skill = CreateSkill("cd_skill", cooldown: 1, apCost: 1);
        m_casterExecutor.Execute(skill, m_targetObj);

        m_casterExecutor.AdvanceCooldowns();
        Assert.AreEqual(0, m_casterExecutor.GetCooldownRemaining("cd_skill"));
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void ResetCooldowns_ClearsAllCooldowns()
    {
        var skill = CreateSkill("cd_skill", cooldown: 3, apCost: 1);
        m_casterExecutor.Execute(skill, m_targetObj);

        m_casterExecutor.ResetCooldowns();
        Assert.AreEqual(0, m_casterExecutor.GetCooldownRemaining("cd_skill"));
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_SucceedsAfterCooldownExpires()
    {
        var skill = CreateSkill("cd_skill", cooldown: 1, apCost: 1);
        m_casterExecutor.Execute(skill, m_targetObj);

        // On cooldown
        Assert.IsFalse(m_casterExecutor.CanCast(skill, m_targetObj).IsSuccess);

        // Advance past cooldown
        m_casterExecutor.AdvanceCooldowns();
        Assert.IsTrue(m_casterExecutor.CanCast(skill, m_targetObj).IsSuccess);
        Object.DestroyImmediate(skill);
    }

    // ── Target Capabilities ──────────────────────────────────────

    [Test]
    public void DamageEffect_HasRequiredCapability_Damageable()
    {
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        Assert.AreEqual(ETargetCapability.Damageable, fx.RequiredCapability);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void HealEffect_HasRequiredCapability_Healable()
    {
        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        Assert.AreEqual(ETargetCapability.Healable, fx.RequiredCapability);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void AddStatusEffect_HasRequiredCapability_Statusable()
    {
        var fx = ScriptableObject.CreateInstance<AddStatusEffectDefinition>();
        Assert.AreEqual(ETargetCapability.Statusable, fx.RequiredCapability);
        Object.DestroyImmediate(fx);
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

    // ── CR-0008: Self target resolution ────────────────────────────

    [Test]
    public void CanCast_Self_ResolvesTargetToCaster_WithNullTarget()
    {
        // Self skills must resolve target to caster, not bail early
        m_casterExecutor.SetCapabilities(ETargetCapability.Damageable | ETargetCapability.Healable);
        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        SetEffectAmount(fx, 10);
        var skill = CreateSkill("self_heal", targetType: ESkillTargetType.Self);
        SetEffectsOnSkill(skill, fx);

        var result = m_casterExecutor.CanCast(skill, null);
        Assert.IsTrue(result.IsSuccess,
            "Self skill with null target should resolve to caster and pass capability check.");

        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void CanCast_Self_Fails_WhenCasterLacksCapability()
    {
        // Caster has only Damageable; HealEffect requires Healable
        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        SetEffectAmount(fx, 10);
        var skill = CreateSkill("self_heal", targetType: ESkillTargetType.Self);
        SetEffectsOnSkill(skill, fx);

        var result = m_casterExecutor.CanCast(skill, null);
        Assert.IsFalse(result.IsSuccess,
            "Self skill should fail when caster lacks required capability for its effects.");
        Assert.AreEqual(ESkillCastFailure.TargetCapabilityBlocked, result.Failure);

        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void Execute_Self_AppliesHealToCaster()
    {
        m_casterUnit.SetMaxHP(100);
        m_casterUnit.SetCurrentHP(40);
        m_casterExecutor.SetCapabilities(ETargetCapability.Damageable | ETargetCapability.Healable);

        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        SetEffectAmount(fx, 15);
        var skill = CreateSkill("self_heal", apCost: 1, targetType: ESkillTargetType.Self);
        SetEffectsOnSkill(skill, fx);

        var result = m_casterExecutor.Execute(skill, null);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(55, m_casterUnit.CurrentHP,
            "Self heal should apply to caster when target is null.");

        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void Execute_Self_Fails_WhenApInsufficient()
    {
        m_casterUnit.SetCurrentAP(0);
        m_casterExecutor.SetCapabilities(ETargetCapability.Damageable | ETargetCapability.Healable);

        var skill = CreateSkill("self_buff", apCost: 2, targetType: ESkillTargetType.Self);
        var result = m_casterExecutor.Execute(skill, null);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.InsufficientAp, result.Failure);

        Object.DestroyImmediate(skill);
    }

    // ── CR-0009: Tag conditions ────────────────────────────────────

    [Test]
    public void CanCast_Fails_WhenCasterLacksRequiredTag()
    {
        m_targetUnit.SetFaction(EFaction.Enemy); // pass faction check
        var skill = CreateSkill("tagged_skill");
        SetSkillTagConditions(skill,
            requiredCaster: new[] { "Element.Fire" });

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TagConditionFailed, result.Failure);

        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_Fails_WhenCasterHasBlockedTag()
    {
        m_targetUnit.SetFaction(EFaction.Enemy); // pass faction check
        var tagComp = m_casterObj.AddComponent<GameplayTagComponent>();
        tagComp.AddTag("State.Silenced");

        var skill = CreateSkill("tagged_skill");
        SetSkillTagConditions(skill,
            blockedCaster: new[] { "State.Silenced" });

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TagConditionFailed, result.Failure);

        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_Fails_WhenTargetLacksRequiredTag()
    {
        m_targetUnit.SetFaction(EFaction.Enemy); // pass faction check
        var skill = CreateSkill("tagged_skill");
        SetSkillTagConditions(skill,
            requiredTarget: new[] { "Target.Unit" });

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TagConditionFailed, result.Failure);

        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_Fails_WhenTargetHasBlockedTag()
    {
        m_targetUnit.SetFaction(EFaction.Enemy); // pass faction check
        var tagComp = m_targetObj.AddComponent<GameplayTagComponent>();
        tagComp.AddTag("State.Immune.Physical");

        var skill = CreateSkill("tagged_skill");
        SetSkillTagConditions(skill,
            blockedTarget: new[] { "State.Immune.Physical" });

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TagConditionFailed, result.Failure);

        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_Succeeds_WhenTagConditionsMet()
    {
        m_targetUnit.SetFaction(EFaction.Enemy); // pass faction check
        var casterTagComp = m_casterObj.AddComponent<GameplayTagComponent>();
        casterTagComp.AddTag("Element.Fire");
        var targetTagComp = m_targetObj.AddComponent<GameplayTagComponent>();
        targetTagComp.AddTag("Target.Unit");

        var skill = CreateSkill("tagged_skill");
        SetSkillTagConditions(skill,
            requiredCaster: new[] { "Element.Fire" },
            requiredTarget: new[] { "Target.Unit" },
            blockedTarget: new[] { "State.Immune.Physical" });

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsTrue(result.IsSuccess);

        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_FactionTagAutoSync_AllowsFactionBasedTargeting()
    {
        m_targetUnit.SetFaction(EFaction.Enemy);

        // Skill requires target to have Faction.Enemy tag
        var skill = CreateSkill("faction_attack", targetType: ESkillTargetType.SingleEnemy);
        SetSkillTagConditions(skill,
            requiredTarget: new[] { "Faction.Enemy" });

        var result = m_casterExecutor.CanCast(skill, m_targetObj);
        Assert.IsTrue(result.IsSuccess,
            "Faction.Enemy tag should be auto-synced from ICombatUnit.Faction for Tag checks.");

        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCast_Self_RespectsCasterTagConditions()
    {
        m_casterExecutor.SetCapabilities(ETargetCapability.Damageable | ETargetCapability.Healable);
        var tagComp = m_casterObj.AddComponent<GameplayTagComponent>();
        tagComp.AddTag("State.Silenced");

        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        SetEffectAmount(fx, 10);
        var skill = CreateSkill("self_heal", targetType: ESkillTargetType.Self);
        SetEffectsOnSkill(skill, fx);
        SetSkillTagConditions(skill,
            blockedCaster: new[] { "State.Silenced" });

        var result = m_casterExecutor.CanCast(skill, null);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TagConditionFailed, result.Failure);

        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    // ── CR-0002: GroundPoint skills (movement as skill) ────────────

    [Test]
    public void CanCastGroundPoint_Fails_WhenSkillIsNull()
    {
        var result = m_casterExecutor.CanCastGroundPoint(null, Vector3.zero, null);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetInvalid, result.Failure);
    }

    [Test]
    public void CanCastGroundPoint_Fails_WhenCasterDead()
    {
        m_casterUnit.SetAlive(false);
        var skill = CreateSkill("move_skill", targetType: ESkillTargetType.GroundPoint);
        var result = m_casterExecutor.CanCastGroundPoint(skill, Vector3.zero, null);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.CasterDead, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCastGroundPoint_Fails_WhenOnCooldown()
    {
        // Use a regular skill to set cooldown on "move_skill" id, then test CanCastGroundPoint
        var regularSkill = CreateSkill("move_skill", cooldown: 2, apCost: 0);
        m_casterExecutor.Execute(regularSkill, m_targetObj); // succeeds → sets cooldown
        Object.DestroyImmediate(regularSkill);

        var gpSkill = CreateSkill("move_skill", cooldown: 2, targetType: ESkillTargetType.GroundPoint);
        var result = m_casterExecutor.CanCastGroundPoint(gpSkill, Vector3.zero, null);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.OnCooldown, result.Failure);
        Object.DestroyImmediate(gpSkill);
    }

    [Test]
    public void CanCastGroundPoint_Fails_WhenCasterTagBlocked()
    {
        var tagComp = m_casterObj.AddComponent<GameplayTagComponent>();
        tagComp.AddTag("State.Rooted");

        var skill = CreateSkill("move_skill", targetType: ESkillTargetType.GroundPoint);
        SetSkillTagConditions(skill,
            blockedCaster: new[] { "State.Rooted" });

        var result = m_casterExecutor.CanCastGroundPoint(skill, Vector3.zero, null);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TagConditionFailed, result.Failure);

        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCastGroundPoint_Fails_WhenNotGroundPointType()
    {
        var skill = CreateSkill("not_move", targetType: ESkillTargetType.Self);
        var result = m_casterExecutor.CanCastGroundPoint(skill, Vector3.zero, null);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetInvalid, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCastGroundPoint_Fails_WhenPathIsNull()
    {
        var skill = CreateSkill("move_skill", targetType: ESkillTargetType.GroundPoint);
        var result = m_casterExecutor.CanCastGroundPoint(skill, Vector3.zero, null);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetInvalid, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCastGroundPoint_Fails_WhenPathStatusInvalid()
    {
        var skill = CreateSkill("move_skill", targetType: ESkillTargetType.GroundPoint);
        // Create a NavMeshPath — in EditMode, it will have PathInvalid status
        var path = new NavMeshPath();
        var result = m_casterExecutor.CanCastGroundPoint(skill, Vector3.zero, path);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetInvalid, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCastGroundPoint_Fails_WhenInsufficientAp_ForSkillCost()
    {
        m_casterUnit.SetCurrentAP(0);
        var skill = CreateSkill("move_skill", apCost: 2, targetType: ESkillTargetType.GroundPoint);
        var result = m_casterExecutor.CanCastGroundPoint(skill, Vector3.zero, null);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.InsufficientAp, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void ExecuteGroundPoint_Fails_WhenCanCastFails()
    {
        var skill = CreateSkill("move_skill", targetType: ESkillTargetType.GroundPoint);
        var result = m_casterExecutor.ExecuteGroundPoint(skill, Vector3.zero, null);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetInvalid, result.Failure);
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void CanCastGroundPoint_CasterRequiresTag_Works()
    {
        var tagComp = m_casterObj.AddComponent<GameplayTagComponent>();
        tagComp.AddTag("Element.Wind");

        var skill = CreateSkill("dash_skill", targetType: ESkillTargetType.GroundPoint);
        SetSkillTagConditions(skill,
            requiredCaster: new[] { "Element.Wind" });

        // Will fail at path validation (null path), but tag check passes
        var result = m_casterExecutor.CanCastGroundPoint(skill, Vector3.zero, null);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ESkillCastFailure.TargetInvalid, result.Failure); // path check, not tag check
        Object.DestroyImmediate(skill);
    }

    // ── Effect Apply ─────────────────────────────────────────────

    [Test]
    public void DamageEffect_Apply_DealsDamage()
    {
        m_targetUnit.SetMaxHP(100);
        m_targetUnit.SetCurrentHP(100);

        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        SetEffectAmount(fx, 30);

        var ctx = new EffectContext
        {
            Caster = m_casterObj,
            Target = m_targetObj,
            CasterExecutor = m_casterExecutor,
            TargetExecutor = m_targetExecutor,
        };
        fx.Apply(ctx);

        Assert.AreEqual(70, m_targetUnit.CurrentHP);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void HealEffect_Apply_HealsTarget()
    {
        m_targetUnit.SetMaxHP(100);
        m_targetUnit.SetCurrentHP(40);
        m_targetExecutor.SetCapabilities(ETargetCapability.Healable);

        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        SetEffectAmount(fx, 15);

        var ctx = new EffectContext
        {
            Caster = m_casterObj,
            Target = m_targetObj,
            CasterExecutor = m_casterExecutor,
            TargetExecutor = m_targetExecutor,
        };
        fx.Apply(ctx);

        Assert.AreEqual(55, m_targetUnit.CurrentHP);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void DamageEffect_Apply_DoesNothingWhenTargetNull()
    {
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        SetEffectAmount(fx, 30);

        var ctx = new EffectContext { Caster = m_casterObj, Target = null };
        // Should not throw
        fx.Apply(ctx);

        Object.DestroyImmediate(fx);
    }

    [Test]
    public void HealEffect_DoesNotHealDeadTarget()
    {
        m_targetUnit.SetAlive(false);
        m_targetUnit.SetCurrentHP(0);

        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        SetEffectAmount(fx, 20);

        var ctx = new EffectContext
        {
            Caster = m_casterObj,
            Target = m_targetObj,
            CasterExecutor = m_casterExecutor,
            TargetExecutor = m_targetExecutor,
        };
        fx.Apply(ctx);

        Assert.AreEqual(0, m_targetUnit.CurrentHP);
        Object.DestroyImmediate(fx);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static SkillDefinition CreateSkill(
        string id,
        int apCost = 1,
        int cooldown = 0,
        ESkillTargetType targetType = ESkillTargetType.SingleEnemy)
    {
        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        var so = new SerializedObject(skill);
        so.FindProperty("m_id").stringValue = id;
        so.FindProperty("m_apCost").intValue = apCost;
        so.FindProperty("m_cooldown").intValue = cooldown;
        so.FindProperty("m_targetType").enumValueIndex = (int)targetType;
        so.ApplyModifiedProperties();
        return skill;
    }

    private static void SetEffectsOnSkill(SkillDefinition skill, EffectDefinition fx)
    {
        var so = new SerializedObject(skill);
        var prop = so.FindProperty("m_effects");
        prop.arraySize = 1;
        prop.GetArrayElementAtIndex(0).objectReferenceValue = fx;
        so.ApplyModifiedProperties();
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

    private static void SetEffectAmount(EffectDefinition fx, int amount)
    {
        var so = new SerializedObject(fx);
        var prop = so.FindProperty("m_amount");
        if (prop != null)
        {
            prop.intValue = amount;
            so.ApplyModifiedProperties();
        }
    }

    // ── Test ICombatUnit implementation ──────────────────────────

    private class TestCombatUnit : MonoBehaviour, ICombatUnit
    {
        private int m_currentHP = 100;
        private int m_maxHP = 100;
        private bool m_isAlive = true;
        private int m_currentAP = 6;

        public EFaction Faction { get; private set; } = EFaction.Player;
        public string DisplayName => "TestUnit";
        public int Initiative => 10;
        public int MaxAP => 6;
        public int CurrentAP => m_currentAP;
        public int MaxHP => m_maxHP;
        public int CurrentHP => m_currentHP;
        public bool IsAlive => m_isAlive;
        public bool IsMoving => false;
        public bool HasEndedRound => false;
        public float MoveSpeedMetersPerAp => 2f;
        public float RemainingMoveDistance => Mathf.Max(0f, m_currentAP * MoveSpeedMetersPerAp);

        public void SetAlive(bool alive) => m_isAlive = alive;
        public void SetCurrentAP(int ap) => m_currentAP = ap;
        public void SetMaxHP(int hp) => m_maxHP = hp;
        public void SetCurrentHP(int hp) => m_currentHP = hp;
        public void SetFaction(EFaction faction) => Faction = faction;

        public void BeginRound() => m_currentAP = 6;
        public bool TryEndRound() => true;
        public bool TryStartMove(NavMeshPath path) => false; // no NavMesh in EditMode tests
        public bool TrySpendAP(int amount)
        {
            if (amount <= 0 || amount > m_currentAP) return false;
            m_currentAP -= amount;
            return true;
        }

        public void TakeDamage(int damage)
        {
            if (!m_isAlive) return;
            m_currentHP = Mathf.Max(0, m_currentHP - damage);
        }

        public void Heal(int amount)
        {
            if (!m_isAlive || amount <= 0) return;
            m_currentHP = Mathf.Min(m_maxHP, m_currentHP + amount);
        }

        public void SetVisualState(EPlayerVisualState state) { }
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

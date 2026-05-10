using MiniChess.Combat;
using MiniChess.Combat.Skills;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
    public void CanCast_Succeeds_WhenTargetHasNoSkillExecutor()
    {
        // Target without SkillExecutor should pass capability check
        var objNoExecutor = new GameObject("NoExecutorTarget");
        objNoExecutor.AddComponent<TestCombatUnit>().SetAlive(true);

        var skill = CreateSkill("test_skill");
        var fx = ScriptableObject.CreateInstance<DamageEffectDefinition>();
        SetEffectAmount(fx, 20);
        SetEffectsOnSkill(skill, fx);

        var result = m_casterExecutor.CanCast(skill, objNoExecutor);
        Assert.IsTrue(result.IsSuccess);

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
        m_targetUnit.SetMaxHP(100);
        m_targetUnit.SetCurrentHP(50);
        m_targetExecutor.SetCapabilities(ETargetCapability.Healable);

        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        SetEffectAmount(fx, 20);
        var skill = CreateSkill("test_skill", apCost: 1);
        SetEffectsOnSkill(skill, fx);

        m_casterExecutor.Execute(skill, m_targetObj);

        Assert.AreEqual(70, m_targetUnit.CurrentHP);
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(fx);
    }

    [Test]
    public void Execute_HealDoesNotExceedMaxHP()
    {
        m_targetUnit.SetMaxHP(100);
        m_targetUnit.SetCurrentHP(95);
        m_targetExecutor.SetCapabilities(ETargetCapability.Healable);

        var fx = ScriptableObject.CreateInstance<HealEffectDefinition>();
        SetEffectAmount(fx, 20);
        var skill = CreateSkill("test_skill", apCost: 1);
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

        public EFaction Faction => EFaction.Player;
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

        public void SetAlive(bool alive) => m_isAlive = alive;
        public void SetCurrentAP(int ap) => m_currentAP = ap;
        public void SetMaxHP(int hp) => m_maxHP = hp;
        public void SetCurrentHP(int hp) => m_currentHP = hp;

        public void BeginRound() => m_currentAP = 6;
        public bool TryEndRound() => true;
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

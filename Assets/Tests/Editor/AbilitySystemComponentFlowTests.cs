using System.Reflection;
using MiniChess.Combat;
using MiniChess.Combat.Skills;
using MiniChess.GameplayTags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Tests.EditMode
{
    public class AbilitySystemComponentFlowTests
    {
        private const BindingFlags k_FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject m_caster;
        private GameObject m_target;

        [TearDown]
        public void TearDown()
        {
            DestroyIfExists(m_caster);
            DestroyIfExists(m_target);
        }

        [Test]
        public void CostBlockedTagChecksCasterAndPreventsEffects()
        {
            var casterAsc = CreateUnit("Caster", 100f, 1f, out _, out var casterTags);
            var targetAsc = CreateUnit("Target", 100f, 0f, out var targetAttributes, out _);
            casterTags.AddTag(new GameplayTag("State.APBlocked"), this);

            var ability = CreateSingleTargetAbility(
                CreateSpendApEffect(blockedTags: new[] { new GameplayTag("State.APBlocked") }),
                CreateModifyHpEffect(-20f));
            var spec = CreateSpec("test_ability", ability);

            var result = casterAsc.Execute(spec, targetAsc.gameObject);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(ESkillCastFailure.TagConditionFailed, result.Failure);
            Assert.AreEqual(100f, targetAttributes.Get(WellKnownAttributeTags.HP));
        }

        [Test]
        public void StandardTargetAbilityDoesNotApplyEffectsWhenCostCannotBePaid()
        {
            var casterAsc = CreateUnit("Caster", 100f, 0f, out var casterAttributes, out _);
            var targetAsc = CreateUnit("Target", 100f, 0f, out var targetAttributes, out _);
            var ability = CreateSingleTargetAbility(CreateSpendApEffect(), CreateModifyHpEffect(-20f));
            var spec = CreateSpec("test_ability", ability);

            var result = casterAsc.Execute(spec, targetAsc.gameObject);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(ESkillCastFailure.InsufficientAp, result.Failure);
            Assert.AreEqual(0f, casterAttributes.Get(WellKnownAttributeTags.AP));
            Assert.AreEqual(100f, targetAttributes.Get(WellKnownAttributeTags.HP));
        }

        [Test]
        public void StandardTargetAbilityPaysCostBeforeApplyingEffects()
        {
            var casterAsc = CreateUnit("Caster", 100f, 1f, out var casterAttributes, out _);
            var targetAsc = CreateUnit("Target", 100f, 0f, out var targetAttributes, out _);
            var ability = CreateSingleTargetAbility(CreateSpendApEffect(), CreateModifyHpEffect(-20f));
            var spec = CreateSpec("test_ability", ability);

            var result = casterAsc.Execute(spec, targetAsc.gameObject);

            Assert.IsTrue(result.IsSuccess, result.FailureMessage);
            Assert.AreEqual(0f, casterAttributes.Get(WellKnownAttributeTags.AP));
            Assert.AreEqual(80f, targetAttributes.Get(WellKnownAttributeTags.HP));
        }

        [Test]
        public void PersistentGrantedTagUsesActiveEffectHandleAsSource()
        {
            var targetAsc = CreateUnit("Target", 100f, 0f, out _, out var targetTags);
            var guardedTag = new GameplayTag("State.Guarded");
            var effect = CreatePersistentTagEffect(guardedTag);

            var firstResult = targetAsc.ApplyEffect(effect, m_target);
            var secondResult = targetAsc.ApplyEffect(effect, m_target);

            Assert.IsTrue(firstResult.IsSuccess, firstResult.FailureMessage);
            Assert.IsTrue(secondResult.IsSuccess, secondResult.FailureMessage);
            Assert.AreEqual(2, targetAsc.ActiveEffects.Count);
            Assert.IsTrue(targetTags.HasTag(guardedTag));

            RemoveActiveEffect(targetAsc, targetAsc.ActiveEffects[0]);
            Assert.AreEqual(1, targetAsc.ActiveEffects.Count);
            Assert.IsTrue(targetTags.HasTag(guardedTag));

            RemoveActiveEffect(targetAsc, targetAsc.ActiveEffects[0]);
            Assert.AreEqual(0, targetAsc.ActiveEffects.Count);
            Assert.IsFalse(targetTags.HasTag(guardedTag));
        }

        [Test]
        public void AbilitySpecResolvesAbilityFromDefinition()
        {
            var ability = ScriptableObject.CreateInstance<SimpleTargetAbility>();
            var definition = ScriptableObject.CreateInstance<SkillDefinition>();
            SetField(definition, "m_id", "basic_attack");
            SetField(definition, "m_ability", ability);

            var spec = AbilitySpec.FromDefinition(definition);

            Assert.AreSame(definition, spec.Definition);
            Assert.AreSame(ability, spec.Ability);
            Assert.AreEqual("basic_attack", spec.Id);

            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(ability);
        }

        [Test]
        public void StandardTargetAbilityRegistersPersistentEffectsOnTargetAsc()
        {
            var casterAsc = CreateUnit("Caster", 100f, 1f, out _, out _);
            var targetAsc = CreateUnit("Target", 100f, 0f, out _, out var targetTags);
            var guardedTag = new GameplayTag("State.Guarded");
            var ability = CreateSingleTargetAbility(null, CreatePersistentTagEffect(guardedTag));
            var spec = CreateSpec("guarding_shout", ability);

            var result = casterAsc.Execute(spec, targetAsc.gameObject);

            Assert.IsTrue(result.IsSuccess, result.FailureMessage);
            Assert.AreEqual(1, targetAsc.ActiveEffects.Count);
            Assert.IsTrue(targetTags.HasTag(guardedTag));
        }

        [Test]
        public void AbilitySystemComponentUsesSkillDefinitionsAsAvailableAbilities()
        {
            var asc = CreateUnit("Caster", 100f, 1f, out _, out _);
            var ability = ScriptableObject.CreateInstance<SimpleTargetAbility>();
            var definition = CreateDefinition("basic_attack", ability);

            asc.SetSkillDefinitions(new[] { definition });

            var spec = asc.FindAbility("basic_attack");
            Assert.IsNotNull(spec);
            Assert.AreSame(definition, spec.Definition);
            Assert.AreSame(ability, spec.Ability);
        }

        [Test]
        public void UnitTurnHandlerSelectsFirstHumanUnitAndActivatesMove()
        {
            var moveAbility = ScriptableObject.CreateInstance<GroundMoveAbility>();
            var moveDefinition = CreateDefinition("basic_move", moveAbility);
            var playerAsc = CreateUnit("Player", 100f, 6f, out _, out var tags);
            playerAsc.gameObject.AddComponent<CombatUnit>();
            tags.AddTag(new GameplayTag("Control.Human"), this);
            playerAsc.SetSkillDefinitions(new[] { moveDefinition });

            var handlerObject = new GameObject("UnitTurnHandler");
            var handler = handlerObject.AddComponent<UnitTurnHandler>();

            Assert.IsTrue(handler.TrySelectDefaultPlayerUnit());
            Assert.AreSame(playerAsc.gameObject, handler.SelectedUnit);
            Assert.IsNotNull(playerAsc.ActiveAbility);
            Assert.AreEqual("basic_move", playerAsc.ActiveAbility.Id);

            Object.DestroyImmediate(handlerObject);
            Object.DestroyImmediate(moveDefinition);
            Object.DestroyImmediate(moveAbility);
        }

        [Test]
        public void SpendApFunctionUsesExecutionPathLengthForMoveCost()
        {
            var casterAsc = CreateUnit("Caster", 100f, 6f, out var attributes, out _);
            var function = ScriptableObject.CreateInstance<SpendAPFunction>();
            var effect = ScriptableObject.CreateInstance<SkillEffect>();
            SetField(effect, "m_function", function);
            SetField(effect, "m_targetMapping", ESkillEffectTarget.Caster);

            var context = new SkillEffectContext
            {
                Caster = casterAsc.gameObject,
                Target = casterAsc.gameObject,
                CasterExecutor = casterAsc,
                TargetExecutor = casterAsc,
                PathLength = 4.1f,
            };

            var computed = effect.Compute(context);
            var applied = effect.Apply(context, computed);

            Assert.IsTrue(computed.IsSuccess, computed.FailureMessage);
            Assert.AreEqual(2f, computed.ComputedValue);
            Assert.IsTrue(applied.IsSuccess, applied.FailureMessage);
            Assert.AreEqual(4f, attributes.Get(WellKnownAttributeTags.AP));

            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(function);
        }

        private AbilitySystemComponent CreateUnit(
            string unitName,
            float hp,
            float ap,
            out AttributeSet attributes,
            out GameplayTagComponent tags)
        {
            var unit = new GameObject(unitName);
            if (m_caster == null)
                m_caster = unit;
            else
                m_target = unit;

            tags = unit.AddComponent<GameplayTagComponent>();
            attributes = unit.AddComponent<AttributeSet>();
            attributes.Testing_AddAttribute(WellKnownAttributeTags.HP, hp, hp);
            attributes.Testing_AddAttribute(WellKnownAttributeTags.AP, ap, 6f);
            attributes.Testing_AddAttribute(WellKnownAttributeTags.MoveSpeed, 2f, 0f);
            attributes.Testing_AddAttribute(WellKnownAttributeTags.Initiative, 1f, 0f);

            return unit.AddComponent<AbilitySystemComponent>();
        }

        private static SimpleTargetAbility CreateSingleTargetAbility(SkillEffect cost, SkillEffect effect)
        {
            var ability = ScriptableObject.CreateInstance<SimpleTargetAbility>();
            if (cost != null)
                SetField(ability, "m_costs", new[] { cost });
            if (effect != null)
                SetField(ability, "m_effects", new[] { effect });
            return ability;
        }

        private static AbilitySpec CreateSpec(string id, SkillAbility ability)
        {
            return AbilitySpec.FromDefinition(CreateDefinition(id, ability));
        }

        private static SkillDefinition CreateDefinition(string id, SkillAbility ability)
        {
            var definition = ScriptableObject.CreateInstance<SkillDefinition>();
            SetField(definition, "m_id", id);
            SetField(definition, "m_ability", ability);
            return definition;
        }

        private static SkillEffect CreateSpendApEffect(GameplayTag[] blockedTags = null)
        {
            var function = ScriptableObject.CreateInstance<SpendAPFunction>();
            SetField(function, "m_fallbackAmount", 1f);

            var effect = ScriptableObject.CreateInstance<SkillEffect>();
            SetField(effect, "m_function", function);
            SetField(effect, "m_targetMapping", ESkillEffectTarget.Caster);
            if (blockedTags != null)
                SetField(effect, "m_blockedTags", blockedTags);

            return effect;
        }

        private static SkillEffect CreateModifyHpEffect(float amount)
        {
            var function = ScriptableObject.CreateInstance<ModifyAttributeFunction>();
            SetField(function, "m_amount", amount);
            SetField(function, "m_attributeTag", WellKnownAttributeTags.HP);

            var effect = ScriptableObject.CreateInstance<SkillEffect>();
            SetField(effect, "m_function", function);
            SetField(effect, "m_targetMapping", ESkillEffectTarget.Target);
            return effect;
        }

        private static SkillEffect CreatePersistentTagEffect(GameplayTag grantedTag)
        {
            var function = ScriptableObject.CreateInstance<TagOnlyEffectFunction>();
            var effect = ScriptableObject.CreateInstance<SkillEffect>();
            SetField(effect, "m_function", function);
            SetField(effect, "m_duration", ESkillEffectDuration.Persistent);
            SetField(effect, "m_durationRounds", 2);
            SetField(effect, "m_targetMapping", ESkillEffectTarget.Target);
            SetField(effect, "m_grantedTags", new[] { grantedTag });
            return effect;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, k_FieldFlags);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail($"Field '{fieldName}' was not found on {target.GetType().Name}.");
        }

        private static void RemoveActiveEffect(AbilitySystemComponent asc, ActiveSkillEffect active)
        {
            var method = typeof(AbilitySystemComponent).GetMethod("RemoveActiveEffect", k_FieldFlags);
            Assert.IsNotNull(method);
            method.Invoke(asc, new object[] { active });
        }

        private static void DestroyIfExists(Object obj)
        {
            if (obj != null)
                Object.DestroyImmediate(obj);
        }
    }
}

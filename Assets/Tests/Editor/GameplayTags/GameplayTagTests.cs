using System;
using NUnit.Framework;
using MiniChess.GameplayTags;

[TestFixture]
public class GameplayTagTests
{
    // ── IsValid ────────────────────────────────────────────────

    [Test]
    public void IsValid_SimpleTag_ReturnsTrue()
    {
        Assert.IsTrue(GameplayTag.IsValid("a"));
        Assert.IsTrue(GameplayTag.IsValid("Status"));
        Assert.IsTrue(GameplayTag.IsValid("Combat.Status.Burning"));
    }

    [Test]
    public void IsValid_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(GameplayTag.IsValid(null));
        Assert.IsFalse(GameplayTag.IsValid(""));
        Assert.IsFalse(GameplayTag.IsValid(" "));
        Assert.IsFalse(GameplayTag.IsValid("\t"));
    }

    [Test]
    public void IsValid_LeadingOrTrailingDot_ReturnsFalse()
    {
        Assert.IsFalse(GameplayTag.IsValid(".a"));
        Assert.IsFalse(GameplayTag.IsValid("a."));
        Assert.IsFalse(GameplayTag.IsValid(".a.b"));
    }

    [Test]
    public void IsValid_DoubleDot_ReturnsFalse()
    {
        Assert.IsFalse(GameplayTag.IsValid("a..b"));
        Assert.IsFalse(GameplayTag.IsValid("a.."));
    }

    [Test]
    public void IsValid_ContainsWhitespace_ReturnsFalse()
    {
        Assert.IsFalse(GameplayTag.IsValid("a b"));
        Assert.IsFalse(GameplayTag.IsValid("a\tb"));
    }

    // ── Constructor ────────────────────────────────────────────

    [Test]
    public void Constructor_ValidValue_CreatesTag()
    {
        var tag = new GameplayTag("Combat.Status.Bleeding");
        Assert.AreEqual("Combat.Status.Bleeding", tag.Value);
    }

    [Test]
    public void Constructor_InvalidValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GameplayTag(""));
        Assert.Throws<ArgumentException>(() => new GameplayTag(".Bad"));
    }

    [Test]
    public void DefaultConstructor_HasEmptyValue()
    {
        var tag = default(GameplayTag);
        Assert.AreEqual(string.Empty, tag.Value);
        Assert.IsFalse(GameplayTag.IsValid(tag.Value));
    }

    // ── Equality ───────────────────────────────────────────────

    [Test]
    public void Equals_SameValue_True()
    {
        var a = new GameplayTag("Status.Burning");
        var b = new GameplayTag("Status.Burning");
        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
    }

    [Test]
    public void Equals_DifferentCase_True()
    {
        var a = new GameplayTag("status.burning");
        var b = new GameplayTag("Status.Burning");
        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
    }

    [Test]
    public void Equals_DifferentValue_False()
    {
        var a = new GameplayTag("Status.Burning");
        var b = new GameplayTag("Status.Bleeding");
        Assert.IsFalse(a.Equals(b));
        Assert.IsFalse(a == b);
        Assert.IsTrue(a != b);
    }

    [Test]
    public void Equals_BoxedObject_Works()
    {
        object a = new GameplayTag("A.B");
        object b = new GameplayTag("A.B");
        Assert.IsTrue(a.Equals(b));
    }

    [Test]
    public void Equals_DifferentType_False()
    {
        var tag = new GameplayTag("A");
        // Must box to object — implicit string→GameplayTag operator would
        // otherwise match IEquatable<GameplayTag>.Equals, defeating the test
        Assert.IsFalse(((object)tag).Equals("different"));
        Assert.IsFalse(tag.Equals((object)null));
    }

    [Test]
    public void GetHashCode_SameValue_SameHash()
    {
        var a = new GameplayTag("Status.Burning");
        var b = new GameplayTag("status.burning");
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    // ── Matches ────────────────────────────────────────────────

    [Test]
    public void Matches_Exact_SameTag_True()
    {
        var tag = new GameplayTag("Status.Burning");
        Assert.IsTrue(tag.Matches(new GameplayTag("Status.Burning"), ETagMatchMode.Exact));
    }

    [Test]
    public void Matches_Exact_DifferentTag_False()
    {
        var tag = new GameplayTag("Status.Burning");
        Assert.IsFalse(tag.Matches(new GameplayTag("Status.Bleeding"), ETagMatchMode.Exact));
    }

    [Test]
    public void Matches_Exact_DifferentCase_True()
    {
        var tag = new GameplayTag("Status.Burning");
        Assert.IsTrue(tag.Matches(new GameplayTag("status.burning"), ETagMatchMode.Exact));
    }

    [Test]
    public void Matches_Prefix_Self_True()
    {
        var tag = new GameplayTag("Combat.Status.Burning");
        Assert.IsTrue(tag.Matches(new GameplayTag("Combat.Status.Burning"), ETagMatchMode.Prefix));
    }

    [Test]
    public void Matches_Prefix_ActualPrefix_True()
    {
        var tag = new GameplayTag("Combat.Status.Burning");
        Assert.IsTrue(tag.Matches(new GameplayTag("Combat.Status"), ETagMatchMode.Prefix));
        Assert.IsTrue(tag.Matches(new GameplayTag("Combat"), ETagMatchMode.Prefix));
    }

    [Test]
    public void Matches_Prefix_QueryLongerThanTarget_False()
    {
        var tag = new GameplayTag("Combat.Status");
        Assert.IsFalse(tag.Matches(new GameplayTag("Combat.Status.Burning"), ETagMatchMode.Prefix));
    }

    [Test]
    public void Matches_Prefix_WrongRoot_False()
    {
        var tag = new GameplayTag("Combat.Status.Burning");
        Assert.IsFalse(tag.Matches(new GameplayTag("Effect.Status"), ETagMatchMode.Prefix));
    }

    [Test]
    public void Matches_Prefix_MidSegment_False()
    {
        var tag = new GameplayTag("Combat.Status");
        Assert.IsFalse(tag.Matches(new GameplayTag("Combat.Stat"), ETagMatchMode.Prefix));
    }

    [Test]
    public void Matches_Prefix_CaseInsensitive()
    {
        var tag = new GameplayTag("Combat.Status.Burning");
        Assert.IsTrue(tag.Matches(new GameplayTag("combat.status"), ETagMatchMode.Prefix));
    }

    // ── Implicit string conversion ─────────────────────────────

    [Test]
    public void ImplicitOperator_StringToTag_Works()
    {
        GameplayTag tag = "Combat.Status.Burning";
        Assert.AreEqual("Combat.Status.Burning", tag.Value);
    }

    [Test]
    public void ImplicitOperator_InvalidString_ThrowsAtConversionTime()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            GameplayTag tag = "";
        });
    }

    // ── GameplayTagRef ─────────────────────────────────────────

    [Test]
    public void TagRef_ValidValue_IsValid()
    {
        var tagRef = new GameplayTagRef("Status.Burning");
        Assert.IsTrue(tagRef.IsValid);
        Assert.IsTrue(tagRef.TryGetTag(out var tag));
        Assert.AreEqual("Status.Burning", tag.Value);
    }

    [Test]
    public void TagRef_InvalidValue_IsNotValid()
    {
        var tagRef = new GameplayTagRef(".Bad");
        Assert.IsFalse(tagRef.IsValid);
        Assert.IsFalse(tagRef.TryGetTag(out _));
    }

    [Test]
    public void TagRef_Default_IsNotValid()
    {
        var tagRef = default(GameplayTagRef);
        Assert.IsFalse(tagRef.IsValid);
        Assert.AreEqual(string.Empty, tagRef.Value);
    }

    [Test]
    public void TagRef_InvalidToTag_Throws()
    {
        var tagRef = new GameplayTagRef("");
        Assert.Throws<InvalidOperationException>(() => tagRef.ToTag());
    }

    [Test]
    public void TagRef_ImplicitToString_Works()
    {
        GameplayTagRef tagRef = "Status.Burning";
        Assert.AreEqual("Status.Burning", tagRef.Value);
    }

    [Test]
    public void TagRef_ImplicitToTag_Works()
    {
        var tagRef = new GameplayTagRef("Status.Burning");
        GameplayTag tag = tagRef; // implicit conversion
        Assert.AreEqual("Status.Burning", tag.Value);
    }

    // ── GameplayTagSet ─────────────────────────────────────────

    [Test]
    public void TagSet_New_IsEmpty()
    {
        var set = new GameplayTagSet();
        Assert.AreEqual(0, set.Count);
        Assert.AreEqual("[empty]", set.ToString());
    }

    [Test]
    public void TagSet_AddAndHas_Exact()
    {
        var set = new GameplayTagSet();
        var tag = new GameplayTag("Status.Burning");
        set.Add(tag);
        Assert.IsTrue(set.Has(tag));
        Assert.AreEqual(1, set.Count);
    }

    [Test]
    public void TagSet_AddAndHas_Prefix()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Combat.Status.Burning"));
        Assert.IsTrue(set.Has(new GameplayTag("Combat"), ETagMatchMode.Prefix));
        Assert.IsTrue(set.Has(new GameplayTag("Combat.Status"), ETagMatchMode.Prefix));
        Assert.IsFalse(set.Has(new GameplayTag("Effect"), ETagMatchMode.Prefix));
    }

    [Test]
    public void TagSet_HasAny_Exact()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Status.Burning"));
        var tags = new[] { new GameplayTag("Status.Bleeding"), new GameplayTag("Status.Burning") };
        Assert.IsTrue(set.HasAny(tags));
    }

    [Test]
    public void TagSet_HasAny_NoneMatch()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Status.Stunned"));
        var tags = new[] { new GameplayTag("Status.Bleeding"), new GameplayTag("Status.Burning") };
        Assert.IsFalse(set.HasAny(tags));
    }

    [Test]
    public void TagSet_HasAll()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Status.Burning"));
        set.Add(new GameplayTag("Combat.Flanking"));
        var tags = new[] { new GameplayTag("Status.Burning"), new GameplayTag("Combat.Flanking") };
        Assert.IsTrue(set.HasAll(tags));
    }

    [Test]
    public void TagSet_HasAll_PartialMatch_False()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Status.Burning"));
        var tags = new[] { new GameplayTag("Status.Burning"), new GameplayTag("Combat.Flanking") };
        Assert.IsFalse(set.HasAll(tags));
    }

    [Test]
    public void TagSet_AddInvalidTag_Throws()
    {
        var set = new GameplayTagSet();
        // default(GameplayTag) bypasses constructor validation — Value is empty
        var invalid = default(GameplayTag);
        Assert.IsFalse(GameplayTag.IsValid(invalid.Value));
        Assert.Throws<ArgumentException>(() => set.Add(invalid));
    }

    [Test]
    public void TagSet_Remove_BySource()
    {
        var set = new GameplayTagSet();
        var tag = new GameplayTag("Status.Burning");
        set.Add(tag, "SourceA");
        set.Add(tag, "SourceB");
        Assert.AreEqual(1, set.Count);

        set.Remove(tag, "SourceA");
        Assert.IsTrue(set.Has(tag)); // still there from SourceB

        set.Remove(tag, "SourceB");
        Assert.IsFalse(set.Has(tag)); // all sources gone
    }

    [Test]
    public void TagSet_Remove_AllSources()
    {
        var set = new GameplayTagSet();
        var tag = new GameplayTag("Status.Burning");
        set.Add(tag, "SourceA");
        set.Add(tag, "SourceB");

        set.Remove(tag); // remove all
        Assert.IsFalse(set.Has(tag));
        Assert.AreEqual(0, set.Count);
    }

    [Test]
    public void TagSet_RemoveAllFromSource()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Status.Burning"), "StatusEffect");
        set.Add(new GameplayTag("Status.Stunned"), "StatusEffect");
        set.Add(new GameplayTag("Combat.Flanking"), "Skill");

        set.RemoveAllFromSource("StatusEffect");
        Assert.AreEqual(1, set.Count);
        Assert.IsTrue(set.Has(new GameplayTag("Combat.Flanking")));
        Assert.IsFalse(set.Has(new GameplayTag("Status.Burning")));
    }

    [Test]
    public void TagSet_Clear()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Status.Burning"));
        set.Add(new GameplayTag("Status.Stunned"));
        set.Clear();
        Assert.AreEqual(0, set.Count);
    }

    [Test]
    public void TagSet_Tags_EnumeratesAll()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("A"));
        set.Add(new GameplayTag("B"));
        int count = 0;
        foreach (var _ in set.Tags) count++;
        Assert.AreEqual(2, count);
    }

    // ── TagQuery ───────────────────────────────────────────────

    [Test]
    public void TagQuery_Empty_IsEmpty()
    {
        var query = new TagQuery();
        Assert.IsTrue(query.IsEmpty);
    }

    [Test]
    public void TagQuery_Evaluate_RequiredAll_Passes()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Status.Burning"));
        set.Add(new GameplayTag("Combat.Flanking"));

        var query = new TagQuery(
            requiredAll: new[] { new GameplayTagRef("Status.Burning"), new GameplayTagRef("Combat.Flanking") }
        );
        Assert.IsTrue(query.Evaluate(set));
    }

    [Test]
    public void TagQuery_Evaluate_RequiredAll_Fails()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Status.Burning"));

        var query = new TagQuery(
            requiredAll: new[] { new GameplayTagRef("Status.Burning"), new GameplayTagRef("Combat.Flanking") }
        );
        Assert.IsFalse(query.Evaluate(set));
    }

    [Test]
    public void TagQuery_Evaluate_BlockedAny_Blocks()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Status.Immune"));

        var query = new TagQuery(
            requiredAll: new[] { new GameplayTagRef("Status.Burning") },
            blockedAny: new[] { new GameplayTagRef("Status.Immune") }
        );
        Assert.IsFalse(query.Evaluate(set));
    }

    [Test]
    public void TagQuery_Evaluate_RequiredAny_Passes()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Status.Burning"));

        var query = new TagQuery(
            requiredAny: new[] { new GameplayTagRef("Status.Burning"), new GameplayTagRef("Status.Stunned") }
        );
        Assert.IsTrue(query.Evaluate(set));
    }

    [Test]
    public void TagQuery_Evaluate_RequiredAny_None_Fails()
    {
        var set = new GameplayTagSet();
        set.Add(new GameplayTag("Status.Bleeding"));

        var query = new TagQuery(
            requiredAny: new[] { new GameplayTagRef("Status.Burning"), new GameplayTagRef("Status.Stunned") }
        );
        Assert.IsFalse(query.Evaluate(set));
    }

    [Test]
    public void TagQuery_Evaluate_NullSet_ReturnsFalse()
    {
        var query = new TagQuery(requiredAll: new[] { new GameplayTagRef("A") });
        Assert.IsFalse(query.Evaluate(null));
    }

    // ── TagEntry ───────────────────────────────────────────────

    [Test]
    public void TagEntry_Constructor_SetsProperties()
    {
        var entry = new TagEntry(
            new GameplayTag("Status.Burning"),
            "Burning",
            "Unit takes fire damage each turn"
        );
        Assert.AreEqual("Status.Burning", entry.Tag.Value);
        Assert.AreEqual("Burning", entry.DisplayName);
        Assert.AreEqual("Unit takes fire damage each turn", entry.Description);
    }
}


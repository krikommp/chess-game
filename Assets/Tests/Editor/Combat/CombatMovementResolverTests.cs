using MiniChess.Combat;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class CombatMovementResolverTests
{
    // ── IsInRange ──────────────────────────────────────────────

    [Test]
    public void IsInRange_TrueWhenWithinRange()
    {
        Assert.IsTrue(CombatMovementResolver.IsInRange(
            new Vector3(0, 0, 0), new Vector3(0, 0, 1f), 1.5f));
    }

    [Test]
    public void IsInRange_TrueWhenExactlyAtRange()
    {
        Assert.IsTrue(CombatMovementResolver.IsInRange(
            new Vector3(0, 0, 0), new Vector3(0, 0, 1.5f), 1.5f));
    }

    [Test]
    public void IsInRange_FalseWhenOutOfRange()
    {
        Assert.IsFalse(CombatMovementResolver.IsInRange(
            new Vector3(0, 0, 0), new Vector3(0, 0, 2f), 1.5f));
    }

    [Test]
    public void IsInRange_Uses3DDistance()
    {
        // (0,0,0) to (0,0,1) is 1m XZ, but Y=5 adds ~5m.
        // Total 3D distance ≈ 5.1m > 1.5m range, so NOT in range.
        Assert.IsFalse(CombatMovementResolver.IsInRange(
            new Vector3(0, 0, 0), new Vector3(0, 5, 1f), 1.5f));

        // Same XZ without Y offset is within range.
        Assert.IsTrue(CombatMovementResolver.IsInRange(
            new Vector3(0, 0, 0), new Vector3(0, 0, 1f), 1.5f));
    }

    // ── FlatSqrDistance ────────────────────────────────────────

    [Test]
    public void FlatSqrDistance_IgnoresY()
    {
        float d = CombatMovementResolver.FlatSqrDistance(
            new Vector3(0, 10, 0), new Vector3(3, 20, 4));
        Assert.AreEqual(25f, d, 0.001f); // 3² + 4² = 25
    }

    [Test]
    public void FlatSqrDistance_ZeroForSamePoint()
    {
        Assert.AreEqual(0f, CombatMovementResolver.FlatSqrDistance(
            new Vector3(1, 2, 3), new Vector3(1, 99, 3)), 0.001f);
    }

    // ── EstimateMoveApCost ─────────────────────────────────────

    [Test]
    public void EstimateMoveApCost_ZeroForZeroLength()
    {
        Assert.AreEqual(0, CombatMovementResolver.EstimateMoveApCost(0f, 2f, 6));
    }

    [Test]
    public void EstimateMoveApCost_RoundsDownAtThreshold()
    {
        // 2m per AP: 1.99m = 0 AP
        Assert.AreEqual(0, CombatMovementResolver.EstimateMoveApCost(1.99f, 2f, 6));
    }

    [Test]
    public void EstimateMoveApCost_OneApAtFullStep()
    {
        // 2m per AP: 2.0m = 1 AP
        Assert.AreEqual(1, CombatMovementResolver.EstimateMoveApCost(2.0f, 2f, 6));
    }

    [Test]
    public void EstimateMoveApCost_ClampedToCurrentAp()
    {
        Assert.AreEqual(2, CombatMovementResolver.EstimateMoveApCost(100f, 2f, 2));
    }

    [Test]
    public void EstimateMoveApCost_ZeroForZeroSpeed()
    {
        Assert.AreEqual(0, CombatMovementResolver.EstimateMoveApCost(10f, 0f, 6));
    }

    // ── TryFindAttackDestination ───────────────────────────────

    [Test]
    public void TryFindAttackDestination_FindsPointOnStraightPath()
    {
        // Path: (0,0,10) → (0,0,5) → (0,0,0) = target at origin
        Vector3[] corners = {
            new Vector3(0, 0, 10),
            new Vector3(0, 0, 5),
            new Vector3(0, 0, 0),
        };

        bool found = CombatMovementResolver.TryFindAttackDestination(
            corners, new Vector3(0, 0, 0), 3f, out Vector3 dest);

        Assert.IsTrue(found);
        // 3m from target along path: target=(0,0,0), 3m back = (0,0,3)
        Assert.AreEqual(0f, dest.x, 0.01f);
        Assert.AreEqual(3f, dest.z, 0.01f);
    }

    [Test]
    public void TryFindAttackDestination_ExactCornerMatch()
    {
        // Exact range equals distance of last segment
        Vector3[] corners = {
            new Vector3(0, 0, 10),
            new Vector3(0, 0, 2),
            new Vector3(0, 0, 0),
        };

        bool found = CombatMovementResolver.TryFindAttackDestination(
            corners, new Vector3(0, 0, 0), 2f, out Vector3 dest);

        Assert.IsTrue(found);
        Assert.AreEqual(0f, dest.x, 0.01f);
        Assert.AreEqual(2f, dest.z, 0.01f);
    }

    [Test]
    public void TryFindAttackDestination_InterpolatesOnSegment()
    {
        // Range falls midway on a segment
        Vector3[] corners = {
            new Vector3(0, 0, 10),
            new Vector3(0, 0, 5),
            new Vector3(0, 0, 0),
        };

        bool found = CombatMovementResolver.TryFindAttackDestination(
            corners, new Vector3(0, 0, 0), 1.5f, out Vector3 dest);

        Assert.IsTrue(found);
        // 1.5m from target = interpolated on the (5→0) segment
        Assert.AreEqual(0f, dest.x, 0.01f);
        Assert.AreEqual(1.5f, dest.z, 0.01f);
    }

    [Test]
    public void TryFindAttackDestination_FindsPointFurtherBack()
    {
        // Range goes beyond a single segment
        Vector3[] corners = {
            new Vector3(0, 0, 20),
            new Vector3(0, 0, 10),
            new Vector3(0, 0, 5),
            new Vector3(0, 0, 0),
        };

        bool found = CombatMovementResolver.TryFindAttackDestination(
            corners, new Vector3(0, 0, 0), 7f, out Vector3 dest);

        Assert.IsTrue(found);
        // From target(0): segment 5→0=5m, need 2m more from 10→5 segment
        // So dest is 2m from corner[2] (5) toward corner[1] (10) = (0,0,7)
        Assert.AreEqual(0f, dest.x, 0.01f);
        Assert.AreEqual(7f, dest.z, 0.01f);
    }

    [Test]
    public void TryFindAttackDestination_ReturnsFalseWhenRangeExceedsTotalPath()
    {
        Vector3[] corners = {
            new Vector3(0, 0, 3),
            new Vector3(0, 0, 2),
            new Vector3(0, 0, 0),
        };

        bool found = CombatMovementResolver.TryFindAttackDestination(
            corners, new Vector3(0, 0, 0), 10f, out Vector3 dest);

        Assert.IsFalse(found);
    }

    [Test]
    public void TryFindAttackDestination_ReturnsFalseForNullCorners()
    {
        Assert.IsFalse(CombatMovementResolver.TryFindAttackDestination(
            null, Vector3.zero, 3f, out _));
    }

    [Test]
    public void TryFindAttackDestination_ReturnsFalseForTooFewCorners()
    {
        Assert.IsFalse(CombatMovementResolver.TryFindAttackDestination(
            new[] { Vector3.zero }, Vector3.zero, 3f, out _));
    }

    // ── TryFindFarthestReachablePoint ──────────────────────────

    [Test]
    public void TryFindFarthestReachablePoint_ClipsAtMaxDistance()
    {
        Vector3[] corners = {
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 5),
            new Vector3(0, 0, 10),
        };

        bool found = CombatMovementResolver.TryFindFarthestReachablePoint(
            corners, 3f, out Vector3 dest);

        Assert.IsTrue(found);
        Assert.AreEqual(0f, dest.x, 0.01f);
        Assert.AreEqual(3f, dest.z, 0.01f);
    }

    [Test]
    public void TryFindFarthestReachablePoint_ReturnsFullPathWhenWithinBudget()
    {
        Vector3[] corners = {
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 3),
        };

        bool found = CombatMovementResolver.TryFindFarthestReachablePoint(
            corners, 10f, out Vector3 dest);

        Assert.IsTrue(found);
        Assert.AreEqual(0f, dest.x, 0.01f);
        Assert.AreEqual(3f, dest.z, 0.01f);
    }

    [Test]
    public void TryFindFarthestReachablePoint_ReturnsFalseForZeroBudget()
    {
        Vector3[] corners = {
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 5),
        };

        Assert.IsFalse(CombatMovementResolver.TryFindFarthestReachablePoint(
            corners, 0f, out _));
    }

    // ── IsDestinationOpen (Vector3 overload) ────────────────────

    [Test]
    public void IsDestinationOpen_TrueWhenNoPositions()
    {
        Assert.IsTrue(CombatMovementResolver.IsDestinationOpen(
            Vector3.zero, 1f, System.Array.Empty<Vector3>()));
    }

    [Test]
    public void IsDestinationOpen_TrueWhenNoConflicts()
    {
        var occupied = new[] {
            new Vector3(0, 0, 10),
            new Vector3(10, 0, 0),
        };

        Assert.IsTrue(CombatMovementResolver.IsDestinationOpen(
            new Vector3(0, 0, 0), 1f, occupied));
    }

    [Test]
    public void IsDestinationOpen_FalseWhenTooClose()
    {
        var occupied = new[] {
            new Vector3(0, 0, 10),
            new Vector3(0.5f, 0, 0),  // only 0.5m away, within 1m radius
        };

        Assert.IsFalse(CombatMovementResolver.IsDestinationOpen(
            new Vector3(0, 0, 0), 1f, occupied));
    }

    [Test]
    public void IsDestinationOpen_IgnoresExcludedPosition()
    {
        var occupied = new[] {
            new Vector3(0, 0, 0),  // exactly at destination, but excluded
        };

        Assert.IsTrue(CombatMovementResolver.IsDestinationOpen(
            new Vector3(0, 0, 0), 1f, occupied, new Vector3(0, 0, 0)));
    }

    [Test]
    public void IsDestinationOpen_YAxisIgnored()
    {
        var occupied = new[] {
            new Vector3(0, 100, 0.9f),  // Y is different but XZ within range
        };

        Assert.IsFalse(CombatMovementResolver.IsDestinationOpen(
            new Vector3(0, 0, 0), 1f, occupied));
    }

    // ── SkillPositioningResult ─────────────────────────────────

    [Test]
    public void Result_AlreadyInRange_HasCorrectFlags()
    {
        var result = CombatMovementResolver.SkillPositioningResult.AlreadyInRange();

        Assert.IsTrue(result.IsAlreadyInRange);
        Assert.IsTrue(result.CanReachRange);
        Assert.IsTrue(result.IsActionable);
        Assert.AreEqual(CombatMovementResolver.PositioningFailure.None, result.Failure);
    }

    [Test]
    public void Result_Fail_SetsFailureOnly()
    {
        var result = CombatMovementResolver.SkillPositioningResult.Fail(
            CombatMovementResolver.PositioningFailure.InsufficientAp);

        Assert.AreEqual(CombatMovementResolver.PositioningFailure.InsufficientAp, result.Failure);
        Assert.IsFalse(result.IsAlreadyInRange);
        Assert.IsFalse(result.CanReachRange);
        Assert.IsFalse(result.HasFallback);
        Assert.IsFalse(result.IsActionable);
    }

    [Test]
    public void Result_Default_HasNoActionableState()
    {
        var result = default(CombatMovementResolver.SkillPositioningResult);

        Assert.IsFalse(result.IsAlreadyInRange);
        Assert.IsFalse(result.CanReachRange);
        Assert.IsFalse(result.HasFallback);
        Assert.IsFalse(result.IsActionable);
        Assert.IsNull(result.BestPath);
    }

    // ── Resolve (EditMode — NavMesh not available) ──────────────

    [Test]
    public void Resolve_ReturnsAlreadyInRange_WhenTargetWithinRange()
    {
        var result = CombatMovementResolver.Resolve(
            casterPosition: new Vector3(0, 0, 0),
            targetPosition: new Vector3(0, 0, 1f),
            skillRange: 1.5f,
            skillApCost: 1,
            currentAp: 6,
            remainingMoveDistance: 12f,
            moveSpeedMetersPerAp: 2f);

        Assert.IsTrue(result.IsAlreadyInRange);
        Assert.IsTrue(result.CanReachRange);
        Assert.AreEqual(CombatMovementResolver.PositioningFailure.None, result.Failure);
    }

    [Test]
    public void Resolve_ReturnsInsufficientAp_WhenNotEnoughAp()
    {
        // With only 1 AP and a far target, can't afford move + skill cost
        var result = CombatMovementResolver.Resolve(
            casterPosition: new Vector3(0, 0, 10f),
            targetPosition: new Vector3(0, 0, 0f),
            skillRange: 1.5f,
            skillApCost: 1,
            currentAp: 1,
            remainingMoveDistance: 0.1f,
            moveSpeedMetersPerAp: 2f);

        // Already-in-range check fails (10m away, 1.5m range)
        // NavMesh check depends on environment. If it fails, result is NavMeshOriginFailed.
        // If NavMesh exists but AP too low, result is InsufficientAp or NoFallbackPossible.
        Assert.IsFalse(result.IsAlreadyInRange);
        Assert.IsFalse(result.CanReachRange);
        Assert.AreNotEqual(CombatMovementResolver.PositioningFailure.None, result.Failure);
    }
}

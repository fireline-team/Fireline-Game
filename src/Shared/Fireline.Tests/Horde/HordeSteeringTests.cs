using System.Collections.Generic;
using System.Numerics;
using Fireline.Shared.Horde;
using NUnit.Framework;

namespace Fireline.Tests.Horde;

public class HordeSteeringTests
{
    private const float Tolerance = 0.0001f;

    [Test]
    public void FindNearest_ReturnsClosestTarget()
    {
        var targets = new List<Vector2> { new(10f, 0f), new(2f, 1f), new(-5f, -5f) };

        int result = HordeSteering.FindNearest(Vector2.Zero, targets);

        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public void FindNearest_NoTargets_ReturnsMinusOne()
    {
        int result = HordeSteering.FindNearest(Vector2.Zero, new List<Vector2>());

        Assert.That(result, Is.EqualTo(-1));
    }


    [Test]
    public void Seek_ReturnsUnitDirectionTowardTarget()
    {
        Vector2 dir = HordeSteering.Seek(Vector2.Zero, new Vector2(3f, 4f), stopDistance: 0.5f);

        Assert.That(dir.X, Is.EqualTo(0.6f).Within(Tolerance));
        Assert.That(dir.Y, Is.EqualTo(0.8f).Within(Tolerance));
    }

    [Test]
    public void Seek_WithinStopDistance_ReturnsZero()
    {
        Vector2 dir = HordeSteering.Seek(Vector2.Zero, new Vector2(0.3f, 0f), stopDistance: 0.5f);

        Assert.That(dir, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void Seek_AlreadyAtTarget_ReturnsZero()
    {
        Vector2 dir = HordeSteering.Seek(Vector2.One, Vector2.One, stopDistance: 0f);

        Assert.That(dir, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void Separation_PushesAwayFromCloseNeighbor()
    {
        var positions = new[] { Vector2.Zero, new Vector2(0.25f, 0f) };

        Vector2 push = HordeSteering.Separation(0, positions, new List<int> { 0, 1 }, radius: 0.5f, maxNeighbors: 8);

        Assert.That(push.X, Is.LessThan(0f), "neighbor is to the right, so push should point left");
        Assert.That(push.Y, Is.EqualTo(0f).Within(Tolerance));
    }

    [Test]
    public void Separation_CloserNeighborPushesHarder()
    {
        var near = new[] { Vector2.Zero, new Vector2(0.1f, 0f) };
        var far = new[] { Vector2.Zero, new Vector2(0.4f, 0f) };
        var ids = new List<int> { 0, 1 };

        float nearPush = HordeSteering.Separation(0, near, ids, 0.5f, 8).Length();
        float farPush = HordeSteering.Separation(0, far, ids, 0.5f, 8).Length();

        Assert.That(nearPush, Is.GreaterThan(farPush));
    }

    [Test]
    public void Separation_IgnoresNeighborsOutsideRadius()
    {
        var positions = new[] { Vector2.Zero, new Vector2(1f, 0f) };

        Vector2 push = HordeSteering.Separation(0, positions, new List<int> { 0, 1 }, radius: 0.5f, maxNeighbors: 8);

        Assert.That(push, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void Separation_OverlappingAgents_PushInOppositeDirections()
    {
        var positions = new[] { Vector2.One, Vector2.One };
        var ids = new List<int> { 0, 1 };

        Vector2 pushA = HordeSteering.Separation(0, positions, ids, 0.5f, 8);
        Vector2 pushB = HordeSteering.Separation(1, positions, ids, 0.5f, 8);

        Assert.That(pushA.Length(), Is.EqualTo(1f).Within(Tolerance));
        Assert.That(pushA + pushB, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void Separation_StopsAtMaxNeighbors()
    {
        var positions = new[] { Vector2.Zero, new Vector2(0.25f, 0f), new Vector2(0.25f, 0f), new Vector2(0.25f, 0f) };
        var ids = new List<int> { 0, 1, 2, 3 };

        float one = HordeSteering.Separation(0, positions, ids, 0.5f, maxNeighbors: 1).Length();
        float three = HordeSteering.Separation(0, positions, ids, 0.5f, maxNeighbors: 8).Length();

        Assert.That(three, Is.EqualTo(one * 3f).Within(Tolerance));
    }
}

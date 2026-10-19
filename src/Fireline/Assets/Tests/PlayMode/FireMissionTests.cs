using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fireline.Missions;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class TestObjectiveStep : MissionObjectiveStep
{
    public override bool IsConfigured => true;
    public override string Instruction => "Test follow-up";
    protected override void OnBegin() { }
    protected override void OnEnd() { }
    public void Finish() => CompleteStep();
}

public class FireMissionTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private GameObject Make(string name, Vector2 position = default)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        objects.Add(go);
        return go;
    }
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private FireObjectiveStep Fires(out FireZone first, out FireZone second)
    {
        first = Make("Required A").AddComponent<FireZone>();
        second = Make("Required B").AddComponent<FireZone>();
        var step = Make("Fire step").AddComponent<FireObjectiveStep>();
        Set(step, "fires", new[] { first, second, first });
        return step;
    }
    private FireRescueMission Mission(MissionObjectiveStep[] steps)
    {
        var spawn = Make("Spawn", new Vector2(100, 100)).AddComponent<MissionPoint>();
        var near = Make("Too close", new Vector2(101, 100)).AddComponent<MissionObjectiveSite>();
        Set(near, "steps", steps);
        var far = Make("Far building", new Vector2(130, 100)).AddComponent<MissionObjectiveSite>();
        Set(far, "steps", steps);
        var mission = Make("Mission").AddComponent<FireRescueMission>();
        Set(mission, "spawnPoints", new[] { spawn });
        Set(mission, "objectivePoints", new[] { near, far });
        Set(mission, "seed", 47);
        Set(mission, "evacuationHoldSeconds", 0.05f);
        return mission;
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var go in objects) if (go != null) Object.Destroy(go);
        objects.Clear();
        yield return null;
    }
    [Test]
    public void PairingUsesGroundDistanceAndBothExclusionRadii()
    {
        var a = Make("A").AddComponent<MissionPoint>();
        var b = Make("B", new Vector2(19, 0)).AddComponent<MissionPoint>();
        b.transform.position += Vector3.forward * 100;
        Assert.That(MissionPoint.CanPair(a, b), Is.False);
        b.transform.position = new Vector3(20, 0, 100);
        Assert.That(MissionPoint.CanPair(a, b), Is.True);
        Assert.That(MissionPoint.CanPair(a, a), Is.False);
    }
    [Test]
    public void OnlyAssignedFiresCountAndRepeatedEventsDoNotCompleteTwice()
    {
        var step = Fires(out var first, out var second);
        var ambient = Make("Ambient").AddComponent<FireZone>();
        int completed = 0;
        step.Completed += () => completed++;
        step.BeginStep();
        Assert.That(step.Remaining, Is.EqualTo(2));
        ambient.ApplyWater(1000);
        first.ApplyWater(1000);
        Assert.That(step.Remaining, Is.EqualTo(1));
        Assert.That(completed, Is.Zero);
        second.ApplyWater(1000);
        second.ApplyWater(1000);
        Assert.That(completed, Is.EqualTo(1));
        step.ResetStep();
        step.BeginStep();
        Assert.That(first.IsBurning && second.IsBurning, Is.True);
        Assert.That(step.Remaining, Is.EqualTo(2));
    }
    [UnityTest]
    public IEnumerator SequentialStepsGateEvacuationAndTeamMustReturnTogether()
    {
        var step = Fires(out var first, out var second);
        var followup = Make("Future step").AddComponent<TestObjectiveStep>();
        var mission = Mission(new MissionObjectiveStep[] { step, followup });
        var p1 = Make("Player 1").AddComponent<PlayerHealth>();
        var p2 = Make("Player 2").AddComponent<PlayerHealth>();
        p1.GetComponent<Rigidbody2D>().gravityScale = 0;
        p2.GetComponent<Rigidbody2D>().gravityScale = 0;
        yield return null;
        Assert.That(mission.Objective.name, Is.EqualTo("Far building"));
        Assert.That(Vector2.Distance(p1.transform.position, mission.Spawn.transform.position), Is.LessThan(1));
        first.ApplyWater(1000);
        second.ApplyWater(1000);
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Objective));
        Assert.That(followup.IsRunning, Is.True);
        p2.transform.position = new Vector3(130, 100, 0);
        followup.Finish();
        yield return new WaitForSeconds(0.1f);
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate));
        p2.transform.position = mission.Spawn.transform.position;
        p2.TakeDamage(1000);
        yield return new WaitForSeconds(0.1f);
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate), "Downed teammates cannot be abandoned.");
        p2.ApplyRevivalWater(p1, 1000);
        yield return new WaitForSeconds(0.1f);
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Complete));
        mission.BeginMission();
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Objective));
        Assert.That(first.IsBurning, Is.True);
        Assert.That(followup.IsComplete, Is.False);
    }
    [UnityTest]
    public IEnumerator NoPlayersCannotAutomaticallyEvacuate()
    {
        var step = Fires(out var first, out var second);
        var mission = Mission(new MissionObjectiveStep[] { step });
        yield return null;
        first.ApplyWater(1000);
        second.ApplyWater(1000);
        yield return new WaitForSeconds(0.1f);
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate));
    }
    [UnityTest]
    public IEnumerator InvalidSetupFailsClearlyRatherThanChoosingNearbyObjective()
    {
        var step = Fires(out _, out _);
        var mission = Mission(new MissionObjectiveStep[] { step });
        var impossible = Make("Same location", new Vector2(100, 100)).AddComponent<MissionObjectiveSite>();
        Set(impossible, "steps", new MissionObjectiveStep[] { step });
        Set(mission, "objectivePoints", new[] { impossible });
        LogAssert.Expect(LogType.Error, "Fire rescue mission has no valid spawn/objective pairs. Check step references and exclusion circles.");
        yield return null;
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.InvalidSetup));
        Assert.That(mission.Spawn, Is.Null);
    }
}

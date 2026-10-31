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
        // Survivors must reach evac before the team can finish.
        yield return new WaitForSeconds(2.1f);
        foreach (var civilian in mission.Objectives[0].Civilians)
            civilian.GetComponent<Rigidbody2D>().position = mission.Spawn.transform.position;
        yield return new WaitForFixedUpdate();
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
    public IEnumerator TwoDistinctObjectivesCanFinishInEitherOrderAndOnlyThenRevealEvacuation()
    {
        var first = Make("First step").AddComponent<TestObjectiveStep>();
        var second = Make("Second step").AddComponent<TestObjectiveStep>();
        var mission = Mission(new MissionObjectiveStep[] { first });
        var other = Make("Other building", new Vector2(160, 100)).AddComponent<MissionObjectiveSite>();
        Set(other, "steps", new MissionObjectiveStep[] { second });
        var sites = (MissionObjectiveSite[])typeof(FireRescueMission).GetField("objectivePoints", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mission);
        Set(mission, "objectivePoints", new[] { sites[1], other, other });
        Set(mission, "objectiveCount", 2);
        var camera = Make("Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10);
        var guidance = mission.gameObject.AddComponent<MissionGuidance>();
        yield return null;
        yield return null;
        Assert.That(mission.Objectives.Count, Is.EqualTo(2));
        Assert.That(mission.Objectives[0], Is.Not.SameAs(mission.Objectives[1]));
        Assert.That(first.IsRunning && second.IsRunning, Is.True);
        var arrow = mission.transform.Find("Mission direction arrows/Evacuation direction");
        Assert.That(arrow.gameObject.activeSelf, Is.False);
        second.Finish();
        Assert.That(mission.CiviliansSaved, Is.Zero);
        Assert.That(mission.CiviliansPendingRelease, Is.EqualTo(3));
        Assert.That(mission.CiviliansWaiting, Is.EqualTo(3));
        yield return null;
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Objective));
        Assert.That(mission.CompletedObjectives, Is.EqualTo(1));
        Assert.That(mission.ShowEvacuationGuidance, Is.False);
        Assert.That(arrow.gameObject.activeSelf, Is.False);
        first.Finish();
        yield return null;
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate));
        Assert.That(mission.ShowEvacuationGuidance, Is.True);
        Assert.That(arrow.gameObject.activeSelf, Is.True);
        mission.BeginMission();
        yield return null;
        Assert.That(mission.CompletedObjectives, Is.Zero);
        Assert.That(mission.ShowEvacuationGuidance, Is.False);
        Assert.That(arrow.gameObject.activeSelf, Is.False);
        first.Finish();
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Objective));
        second.Finish();
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate));
    }

    [Test]
    public void CivilianDeadlineWarnsThenLosesOneAtEachIntervalAndReleasesOnlySurvivors()
    {
        var site = Make("Rescue building").AddComponent<MissionObjectiveSite>();
        Set(site, "firstLossSeconds", 10f);
        Set(site, "lossIntervalSeconds", 5f);
        Set(site, "warningSeconds", 2f);
        site.BeginRescue();
        site.AdvanceRescue(7f);
        Assert.That(site.CiviliansInDanger, Is.False);
        site.AdvanceRescue(1f);
        Assert.That(site.CiviliansInDanger, Is.True);
        site.AdvanceRescue(2f);
        Assert.That(site.CiviliansLost, Is.EqualTo(1));
        Assert.That(site.CiviliansWaiting, Is.EqualTo(2));
        Assert.That(site.SecondsUntilLoss, Is.EqualTo(5f));
        site.AdvanceRescue(5f);
        site.ReleaseCivilians();
        site.ReleaseCivilians();
        site.AdvanceRescue(100f);
        Assert.That(site.CiviliansSaved, Is.Zero);
        Assert.That(site.CiviliansPendingRelease, Is.EqualTo(1));
        Assert.That(site.CiviliansLost, Is.EqualTo(2));
        Assert.That(site.CiviliansWaiting, Is.Zero);
        Assert.That(site.CiviliansInDanger, Is.False);
    }

    [Test]
    public void LargeTimeStepCapsLossesAndPrepareClearsCiviliansAndVisuals()
    {
        var site = Make("Rescue building").AddComponent<MissionObjectiveSite>();
        site.BeginRescue();
        site.AdvanceRescue(1000f);
        site.ReleaseCivilians();
        Assert.That(site.CiviliansLost, Is.EqualTo(3));
        Assert.That(site.CiviliansSaved, Is.Zero);
        site.Prepare();
        Assert.That(site.CiviliansLost, Is.Zero);
        Assert.That(site.GetComponentsInChildren<SpriteRenderer>().Length, Is.Zero);
        site.AdvanceRescue(1000f);
        Assert.That(site.CiviliansLost, Is.Zero);
        site.BeginRescue();
        Assert.That(site.CiviliansWaiting, Is.EqualTo(3));
        Assert.That(site.GetComponentsInChildren<SpriteRenderer>().Length, Is.Zero, "Trapped civilians stay inside.");
    }

    [UnityTest]
    public IEnumerator OnlyFinalStepReleasesCiviliansAndRestartResetsRescue()
    {
        var first = Make("First").AddComponent<TestObjectiveStep>();
        var last = Make("Last").AddComponent<TestObjectiveStep>();
        var mission = Mission(new MissionObjectiveStep[] { first, last });
        yield return null;
        Assert.That(mission.CiviliansWaiting, Is.EqualTo(3));
        first.Finish();
        Assert.That(mission.CiviliansSaved, Is.Zero);
        mission.Objective.AdvanceRescue(120f);
        last.Finish();
        Assert.That(mission.CiviliansSaved, Is.Zero);
        Assert.That(mission.CiviliansPendingRelease, Is.EqualTo(2));
        Assert.That(mission.CiviliansLost, Is.EqualTo(1));
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate));
        Assert.That(mission.RescueNotice, Does.Contain("2 civilians emerging"));
        mission.BeginMission();
        Assert.That(mission.CiviliansWaiting, Is.EqualTo(3));
        Assert.That(mission.CiviliansSaved, Is.Zero);
        Assert.That(mission.CiviliansLost, Is.Zero);
        Assert.That(mission.RescueNotice, Is.Empty);
    }

    [UnityTest]
    public IEnumerator EmptyLobbyPausesCasualtiesAndAllLostStillAllowsObjectiveCompletion()
    {
        var step = Make("Rescue step").AddComponent<TestObjectiveStep>();
        var mission = Mission(new MissionObjectiveStep[] { step });
        yield return null;
        var site = mission.Objective;
        site.AdvanceRescue(119.95f);
        yield return new WaitForSeconds(0.1f);
        Assert.That(site.CiviliansLost, Is.Zero, "An empty lobby must not lose civilians.");
        site.AdvanceRescue(1000f);
        step.Finish();
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate));
        Assert.That(mission.CiviliansSaved, Is.Zero);
        Assert.That(mission.CiviliansLost, Is.EqualTo(3));
        Assert.That(mission.RescueAlert, Is.Empty);
    }

    [UnityTest]
    public IEnumerator SurvivorsEmergeInOrderFollowNearestLivingPlayerAndMustExtract()
    {
        var step = Make("Rescue step").AddComponent<TestObjectiveStep>();
        var mission = Mission(new MissionObjectiveStep[] { step });
        var p1 = Make("Player 1").AddComponent<PlayerHealth>();
        var p2 = Make("Player 2").AddComponent<PlayerHealth>();
        p1.GetComponent<Rigidbody2D>().gravityScale = p2.GetComponent<Rigidbody2D>().gravityScale = 0;
        yield return null;
        var site = mission.Objective;
        Set(site, "releaseIntervalSeconds", 0.3f);
        Assert.That(site.Civilians.Count, Is.Zero);
        step.Finish();
        yield return null;
        yield return null;
        Assert.That(site.Civilians.Count, Is.EqualTo(1));
        Assert.That(mission.CiviliansSaved, Is.Zero);
        var first = site.Civilians[0];
        Vector2 start = first.transform.position;
        p1.GetComponent<Rigidbody2D>().position = start + Vector2.right * 4;
        p2.GetComponent<Rigidbody2D>().position = start + Vector2.left * 8;
        yield return new WaitForSeconds(0.12f);
        Assert.That(first.FollowTarget, Is.SameAs(p1));
        Assert.That(first.transform.position.x, Is.GreaterThan(start.x));
        Assert.That(first.MoveSpeed, Is.EqualTo(4f));
        p1.TakeDamage(1000);
        yield return new WaitForSeconds(0.04f);
        Assert.That(first.FollowTarget, Is.SameAs(p2));
        p2.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.04f);
        Assert.That(first.FollowTarget, Is.Null);
        yield return new WaitForSeconds(0.6f);
        Assert.That(site.Civilians.Count, Is.EqualTo(3));
        Assert.That(mission.CiviliansEscorting, Is.EqualTo(3));
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate));
        first.GetComponent<Rigidbody2D>().position = mission.Spawn.transform.position;
        yield return new WaitForSeconds(0.04f);
        Assert.That(first.IsSaved, Is.True);
        Assert.That(mission.CiviliansSaved, Is.EqualTo(1));
        Assert.That(mission.CiviliansEscorting, Is.EqualTo(2));
        first.TakeDamage(1000);
        Assert.That(mission.CiviliansLost, Is.Zero);
        site.Civilians[1].TakeDamage(1000);
        site.Civilians[1].TakeDamage(1000);
        Assert.That(mission.CiviliansLost, Is.EqualTo(1));
        Assert.That(mission.CiviliansEscorting, Is.EqualTo(1));
        mission.BeginMission();
        yield return null;
        Assert.That(site.Civilians.Count, Is.Zero);
        Assert.That(first == null, Is.True);
        Assert.That(mission.CiviliansWaiting, Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator CivilianContactDamageUsesFireAndEnemyCooldownAndStopsOnDeath()
    {
        var step = Make("Rescue step").AddComponent<TestObjectiveStep>();
        var mission = Mission(new MissionObjectiveStep[] { step });
        yield return null;
        var site = mission.Objective;
        step.Finish();
        yield return null;
        yield return null;
        var civilian = site.Civilians[0];
        Set(civilian, "contactDamageCooldown", 0.1f);
        var fire = Make("Hazard", civilian.transform.position).AddComponent<FireZone>();
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(0.04f);
        Assert.That(civilian.CurrentHealth, Is.EqualTo(40));
        yield return new WaitForSeconds(0.12f);
        Assert.That(civilian.CurrentHealth, Is.EqualTo(30));
        fire.ApplyWater(1000);
        yield return new WaitForSeconds(0.12f);
        Assert.That(civilian.CurrentHealth, Is.EqualTo(30));
        var enemyObject = Make("Enemy", civilian.transform.position);
        enemyObject.SetActive(false);
        var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        var enemy = enemyObject.AddComponent<HordeEnemy>();
        Set(enemy, "definition", definition);
        enemyObject.AddComponent<CircleCollider2D>().isTrigger = true;
        enemyObject.SetActive(true);
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(0.25f);
        Assert.That(civilian.CurrentHealth, Is.LessThan(30));
        yield return new WaitForSeconds(0.2f);
        Assert.That(civilian.IsResolved, Is.True);
        Assert.That(civilian.IsSaved, Is.False);
        Assert.That(mission.CiviliansLost, Is.EqualTo(1));
        Object.Destroy(definition);
    }

    [UnityTest]
    public IEnumerator CivilianRoutesAroundWallAndWalksIntoExtractionBeforeMissionCompletes()
    {
        var step = Make("Rescue step").AddComponent<TestObjectiveStep>();
        var mission = Mission(new MissionObjectiveStep[] { step });
        var player = Make("Player").AddComponent<PlayerHealth>();
        player.GetComponent<Rigidbody2D>().gravityScale = 0;
        yield return null;
        var site = mission.Objective;
        Set(site, "civilianCount", 1);
        mission.BeginMission();
        var wall = Make("Blocking wall", new Vector2(129, 100));
        wall.layer = LayerMask.NameToLayer(Structure.ObstacleLayer);
        var wallCollider = wall.AddComponent<BoxCollider2D>();
        wallCollider.size = new Vector2(1, 5);
        step.Finish();
        yield return null;
        yield return null;
        var civilian = site.Civilians[0];
        civilian.GetComponent<Rigidbody2D>().position = new Vector2(125, 100);
        player.GetComponent<Rigidbody2D>().position = new Vector2(133, 100);
        Physics2D.SyncTransforms();
        float deadline = Time.time + 6;
        while (Vector2.Distance(civilian.transform.position, player.transform.position) > 1 && Time.time < deadline)
        {
            yield return new WaitForFixedUpdate();
            Assert.That(wallCollider.OverlapPoint(civilian.transform.position), Is.False, "Civilian walked through a wall.");
        }
        Assert.That(Vector2.Distance(civilian.transform.position, player.transform.position), Is.LessThan(1.1f));
        player.GetComponent<Rigidbody2D>().position = mission.Spawn.transform.position;
        yield return new WaitForSeconds(0.1f);
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate), "Players cannot abandon a survivor.");
        deadline = Time.time + 12;
        while (!civilian.IsResolved && Time.time < deadline) yield return new WaitForFixedUpdate();
        Assert.That(civilian.IsSaved, Is.True);
        yield return new WaitForSeconds(0.1f);
        Assert.That(mission.CiviliansSaved, Is.EqualTo(1));
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Complete));
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

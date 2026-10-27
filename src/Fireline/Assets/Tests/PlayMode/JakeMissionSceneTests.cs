#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Fireline.Missions;
using Game.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class JakeMissionSceneTests
{
    [UnityTest]
    public IEnumerator AuthoredCandidatesHaveClearSpawnsAndHoseReachableFires()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Scenes/JakeProtoLevel.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        var mission = Object.FindAnyObjectByType<FireRescueMission>();
        Assert.That(mission, Is.Not.Null);
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Objective));
        Assert.That(mission.Objectives.Count, Is.EqualTo(2));
        Assert.That(mission.ShowEvacuationGuidance, Is.False);
        var so = new SerializedObject(mission);
        var spawns = so.FindProperty("spawnPoints");
        var sites = so.FindProperty("objectivePoints");
        Assert.That(spawns.arraySize, Is.EqualTo(3));
        Assert.That(sites.arraySize, Is.GreaterThan(spawns.arraySize));
        Physics2D.SyncTransforms();
        int blockers = LayerMask.GetMask(Structure.ObstacleLayer);
        for (int i = 0; i < spawns.arraySize; i++)
        {
            var spawn = (MissionPoint)spawns.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(Physics2D.OverlapCircle(spawn.transform.position, 1.5f, blockers), Is.Null, spawn.name + " blocked");
        }
        for (int i = 0; i < sites.arraySize; i++)
        {
            var site = (MissionObjectiveSite)sites.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(site.IsConfigured, Is.True, site.name);
            bool hasSpawn = false;
            for (int j = 0; j < spawns.arraySize; j++)
                hasSpawn |= MissionPoint.CanPair((MissionPoint)spawns.GetArrayElementAtIndex(j).objectReferenceValue, site.Point);
            Assert.That(hasSpawn, Is.True, site.name + " has no valid pair");
            foreach (var fire in ((FireObjectiveStep)site.Steps[0]).Fires)
            {
                Vector2 approach = (Vector2)fire.transform.position + Vector2.down * 2;
                Assert.That(Physics2D.OverlapCircle(approach, 0.5f, blockers), Is.Null, site.name + " approach blocked");
                Assert.That(Physics2D.Linecast(approach, fire.transform.position, blockers).collider, Is.Null,
                    site.name + " fire is behind a hose blocker");
                if (!mission.Objectives.Contains(site)) Assert.That(fire.gameObject.activeSelf, Is.False);
                else
                {
                    var upright = fire.GetComponentInChildren<StandUpright>();
                    var mesh = upright.GetComponent<MeshFilter>();
                    Assert.That(mesh, Is.Not.Null);
                    Bounds bounds = mesh.sharedMesh.bounds;
                    Vector3 bottom = upright.transform.TransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                    Assert.That(Vector3.Distance(bottom, fire.transform.position), Is.LessThan(0.001f),
                        "Objective flame base must touch its ground-space collider origin.");
                    Vector3 top = upright.transform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
                    Assert.That(top.z, Is.LessThan(bottom.z), "Flame must rise above the XY ground, toward negative Z.");
                }
            }
        }
        // Join through the real prefab and verify camera/tether initialization preserves the selected spawn.
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab");
        var player = Object.Instantiate(playerPrefab);
        yield return new WaitForSeconds(0.5f);
        Assert.That(Vector2.Distance(player.transform.position, mission.Spawn.transform.position), Is.LessThan(2),
            "Shared camera or hose tether moved the new player away from the mission spawn.");
        // Exercise the same overlap path as the real player hose, not just direct objective events.
        var hoseObject = new GameObject("Mission verification hose");
        var hose = hoseObject.AddComponent<StandardHose>();
        var rules = Object.FindAnyObjectByType<HoseWaterRules>();
        bool required = rules != null && rules.RequireWaterSource;
        if (rules != null) rules.RequireWaterSource = false;
        foreach (var site in mission.Objectives)
        {
            foreach (var fire in ((FireObjectiveStep)mission.GetCurrentStep(site)).Fires.ToArray())
            {
                hoseObject.transform.position = fire.transform.position + Vector3.down * 2;
                Physics2D.SyncTransforms();
                hose.Spray(Vector2.up, 10);
                Assert.That(fire.IsBurning, Is.False, fire.name);
            }
        }
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate));
        if (rules != null) rules.RequireWaterSource = required;
        Object.Destroy(hoseObject);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator ClearScene()
    {
        var oldScene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("After mission test"));
        yield return SceneManager.UnloadSceneAsync(oldScene);
    }
}
#endif

using System.Linq;
using Fireline.Missions;
using Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit, one-time scene authoring. Does not run on load or modify other scenes.</summary>
public static class JakeMissionSetup
{
    [MenuItem("Fireline/Missions/Set up JakeProtoLevel fire mission")]
    public static void Setup()
    {
        const string path = "Assets/_Scenes/JakeProtoLevel.unity";
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(path);
        if (Object.FindAnyObjectByType<FireRescueMission>() != null)
        {
            Debug.Log("JakeProtoLevel already has a mission. Edit its points in the Inspector.");
            return;
        }
        var root = new GameObject("Fire rescue mission");
        var mission = root.AddComponent<FireRescueMission>();
        root.AddComponent<MissionGuidance>();
        var missionSettings = new SerializedObject(mission);
        missionSettings.FindProperty("objectiveCount").intValue = 2;
        missionSettings.ApplyModifiedPropertiesWithoutUndo();
        var spawnsRoot = Child(root.transform, "Spawn and evacuation candidates", Vector3.zero);
        var sitesRoot = Child(root.transform, "Building objective candidates", Vector3.zero);
        var hydrant = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Hydrant.prefab");
        var firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/FireZone/FireZone.prefab");
        var positions = new[] { new Vector3(0, -21, 0), new Vector3(30, -10, 0), new Vector3(-36, 10, 0) };
        var spawns = positions.Select((position, i) =>
        {
            var point = Child(spawnsRoot.transform, $"Spawn {i + 1}", position).AddComponent<MissionPoint>();
            SetFloat(point, "exclusionRadius", 9);
            PlaceHydrant(hydrant, point.transform, position + Vector3.left * 2);
            return point;
        }).ToArray();
        Physics2D.SyncTransforms();
        var buildings = Object.FindObjectsByType<Structure>(FindObjectsSortMode.None)
            .Where(HasClearApproach).OrderBy(x => x.transform.position.x).ToArray();
        var sites = buildings.Select((building, i) =>
        {
            var box = building.GetComponent<BoxCollider2D>();
            // Structure's front edge is its ground-space transform origin.
            Vector3 front = building.transform.position;
            front.z = 0;
            var site = Child(sitesRoot.transform, $"Objective {i + 1} - {building.name}", front + Vector3.down * 1.5f).AddComponent<MissionObjectiveSite>();
            SetFloat(site.Point, "exclusionRadius", 9);
            var step = site.gameObject.AddComponent<FireObjectiveStep>();
            var fireList = new FireZone[3];
            float width = box != null ? box.size.x * Mathf.Abs(building.transform.lossyScale.x) : 8;
            for (int j = 0; j < 3; j++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(firePrefab, scene);
                instance.name = $"Required fire {j + 1}";
                instance.transform.SetParent(site.transform, true);
                instance.transform.position = front + new Vector3((j - 1) * Mathf.Min(width * 0.3f, 3), -0.9f, 0);
                fireList[j] = instance.GetComponent<FireZone>();
                // Colliders stay outside the solid building so the existing hose can reach every fire.
                instance.GetComponent<BoxCollider2D>().size = new Vector2(1.4f, 1.4f);
                instance.SetActive(false);
            }
            SetArray(step, "fires", fireList);
            SetArray(site, "steps", new Object[] { step });
            PlaceHydrant(hydrant, site.transform, front + new Vector3(0, -4, 0));
            Debug.Log($"Mission building {building.name}: {front}, width {width}");
            return site;
        }).ToArray();
        SetArray(mission, "spawnPoints", spawns);
        SetArray(mission, "objectivePoints", sites);
        // The prototype's manual stress UI would obscure mission guidance.
        foreach (var stress in Object.FindObjectsByType<HordeStressTest>(FindObjectsSortMode.None))
        {
            var serialized = new SerializedObject(stress);
            serialized.FindProperty("showPanel").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Authored {spawns.Length} spawn points and {sites.Length} building objectives in JakeProtoLevel.");
    }

    private static bool HasClearApproach(Structure building)
    {
        var box = building.GetComponent<BoxCollider2D>();
        float width = box != null ? box.size.x * Mathf.Abs(building.transform.lossyScale.x) : 8;
        int blockers = LayerMask.GetMask(Structure.ObstacleLayer);
        Vector2 front = building.transform.position;
        for (int j = 0; j < 3; j++)
        {
            Vector2 fire = front + new Vector2((j - 1) * Mathf.Min(width * 0.3f, 3), -0.9f);
            Vector2 approach = fire + Vector2.down * 2;
            if (Physics2D.OverlapCircle(approach, 0.5f, blockers) != null ||
                Physics2D.Linecast(approach, fire, blockers).collider != null) return false;
        }
        return Physics2D.OverlapCircle(front + Vector2.down * 4, 0.75f, blockers) == null;
    }

    private static GameObject Child(Transform parent, string name, Vector3 world)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = world;
        return go;
    }
    private static void PlaceHydrant(GameObject prefab, Transform parent, Vector3 position)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = "Mission water supply";
        go.transform.position = position;
    }
    private static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var array = so.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

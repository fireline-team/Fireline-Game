using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Scene-local water supply. Gives each joining player a separately simulated hose.</summary>
public class HoseRopeSource : MonoBehaviour
{
    [SerializeField] private Transform sourcePoint;
    [SerializeField] private Transform groundPlane;
    [Tooltip("Local up axis of the ground plane. This project's ground is XY, so use (0,0,-1).")]
    [SerializeField] private Vector3 groundNormal = Vector3.back;
    [SerializeField] private HoseRope ropePrefab;

    private readonly Dictionary<PlayerInput, HoseRope> hoses = new Dictionary<PlayerInput, HoseRope>();
    private readonly List<PlayerInput> departed = new List<PlayerInput>();
    public int ConnectedPlayers => hoses.Count;

    private void Update()
    {
        if (sourcePoint == null || ropePrefab == null) return;
        foreach (PlayerInput player in PlayerInput.all)
        {
            if (player.gameObject.scene != gameObject.scene || hoses.ContainsKey(player)) continue;
            HoseWeapon nozzle = GetNozzle(player);
            if (nozzle == null) continue;
            HoseRope rope = Instantiate(ropePrefab, player.transform);
            rope.name = "Supply hose";
            Transform plane = groundPlane != null ? groundPlane : transform;
            rope.Bind(sourcePoint, nozzle.NozzleTransform, plane.position, plane.TransformDirection(groundNormal));
            hoses.Add(player, rope);
        }
        departed.Clear();
        foreach (var pair in hoses)
        {
            if (pair.Key == null || !pair.Key.isActiveAndEnabled || pair.Value == null)
            {
                if (pair.Value != null) Destroy(pair.Value.gameObject);
                departed.Add(pair.Key);
                continue;
            }
            HoseWeapon nozzle = GetNozzle(pair.Key);
            pair.Value.SetNozzle(nozzle != null ? nozzle.NozzleTransform : null);
        }
        foreach (var player in departed) hoses.Remove(player);
    }

    private static HoseWeapon GetNozzle(PlayerInput player)
    {
        HoseLoadout loadout = player.GetComponent<HoseLoadout>();
        return loadout != null ? loadout.Current : player.GetComponent<HoseWeapon>();
    }

    private void OnDisable()
    {
        foreach (var rope in hoses.Values)
            if (rope != null) Destroy(rope.gameObject);
        hoses.Clear();
    }
}

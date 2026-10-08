using System.Collections.Generic;
using UnityEngine;

/// <summary>An interactable outlet. Connection ownership lives on each player.</summary>
public class HoseRopeSource : Interactable
{
    [SerializeField] private Transform sourcePoint;
    [SerializeField] private Transform groundPlane;
    [Tooltip("Local up axis of the ground plane. XY ground uses (0,0,-1).")]
    [SerializeField] private Vector3 groundNormal = Vector3.back;
    [SerializeField] private HoseRope ropePrefab;

    private static readonly List<HoseRopeSource> Sources = new List<HoseRopeSource>();
    private readonly HashSet<PlayerHoseConnection> connections = new HashSet<PlayerHoseConnection>();
    public int ConnectedPlayers => connections.Count;
    public bool IsAvailable => isActiveAndEnabled && sourcePoint != null && ropePrefab != null;

    protected override void OnEnable()
    {
        base.OnEnable();
        Sources.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Sources.Remove(this);
        // Detach mutates connections, so iterate a snapshot.
        foreach (var connection in new List<PlayerHoseConnection>(connections))
            if (connection != null) connection.Detach();
        connections.Clear();
    }

    public override bool CanInteract(PlayerInteractor player)
    {
        if (!IsAvailable || player == null || player.gameObject.scene != gameObject.scene) return false;
        var health = player.GetComponent<PlayerHealth>();
        return (health == null || !health.IsDead) && player.GetComponent<HoseWeapon>() != null;
    }

    public override string GetPrompt(PlayerInteractor player)
    {
        var connection = player.GetComponent<PlayerHoseConnection>();
        if (connection == null || !connection.IsConnected) return "Attach hose";
        return connection.Source == this ? "Detach hose" : "Transfer hose here";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player) || ((Vector2)(player.transform.position - transform.position)).sqrMagnitude
            > InteractRadius * InteractRadius) return;
        var connection = player.GetComponent<PlayerHoseConnection>();
        if (connection == null) connection = player.gameObject.AddComponent<PlayerHoseConnection>();
        if (connection.Source == this) connection.Detach();
        else connection.Attach(this);
    }

    public static HoseRopeSource FindNearest(GameObject player)
    {
        HoseRopeSource best = null;
        float distance = float.PositiveInfinity;
        foreach (var candidate in Sources)
        {
            if (candidate == null || !candidate.IsAvailable || candidate.gameObject.scene != player.scene) continue;
            float next = (candidate.transform.position - player.transform.position).sqrMagnitude;
            if (next < distance) { best = candidate; distance = next; }
        }
        return best;
    }

    public HoseRope CreateRope(PlayerHoseConnection player, Transform nozzle)
    {
        var rope = Instantiate(ropePrefab, player.transform);
        rope.name = "Supply hose";
        var plane = groundPlane != null ? groundPlane : transform;
        rope.Bind(sourcePoint, nozzle, plane.position, plane.TransformDirection(groundNormal));
        connections.Add(player);
        return rope;
    }

    public void Release(PlayerHoseConnection player) => connections.Remove(player);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearSources() => Sources.Clear();
}

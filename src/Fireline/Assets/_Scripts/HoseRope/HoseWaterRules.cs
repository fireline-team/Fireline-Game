using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Scene-wide experiment switch. Scenes without this component keep legacy firing.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-100)]
public class HoseWaterRules : MonoBehaviour
{
    [Tooltip("ON: a water-source connection is required to spray. OFF: water always works, even detached.")]
    [SerializeField] private bool requireWaterSource = true;
    [Tooltip("Connect newly joining players to their nearest source once. Detaching never auto-reconnects.")]
    [SerializeField] private bool startConnected = true;
    private static readonly List<HoseWaterRules> Active = new List<HoseWaterRules>();

    public bool RequireWaterSource { get => requireWaterSource; set => requireWaterSource = value; }

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    public static bool CanSpray(GameObject player)
    {
        foreach (var rules in Active)
        {
            if (rules == null || rules.gameObject.scene != player.scene || !rules.requireWaterSource) continue;
            var connection = player.GetComponent<PlayerHoseConnection>();
            return connection != null && connection.isActiveAndEnabled && connection.IsConnected;
        }
        return true;
    }

    private void Update()
    {
        foreach (var player in PlayerInput.all)
        {
            if (player.gameObject.scene != gameObject.scene) continue;
            var connection = player.GetComponent<PlayerHoseConnection>();
            if (connection != null) continue;
            connection = player.gameObject.AddComponent<PlayerHoseConnection>();
            if (startConnected) connection.Attach(HoseRopeSource.FindNearest(player.gameObject));
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRules() => Active.Clear();
}

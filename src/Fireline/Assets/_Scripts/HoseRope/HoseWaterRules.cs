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
    [Header("Hose tether")]
    [Tooltip("Turn off to restore unlimited payout and unrestricted movement.")]
    [SerializeField] private bool limitHoseLength = true;
    [Tooltip("Maximum source-to-nozzle distance in world units, including nozzle height.")]
    [SerializeField, Min(1f)] private float hoseLength = 12f;
    [Tooltip("Seconds of pushing outward at normal movement speed before the hose breaks.")]
    [SerializeField, Min(0.1f)] private float secondsToBreak = 2f;
    [Tooltip("Seconds to recover from full strain after releasing the pull.")]
    [SerializeField, Min(0.1f)] private float strainRecoveryTime = 1f;
    [Tooltip("Outward speed counted as a full-strength pull. Matches the default player speed.")]
    [SerializeField, Min(0.1f)] private float fullPullSpeed = 5f;

    public bool LimitHoseLength { get => limitHoseLength; set => limitHoseLength = value; }
    public float HoseLength { get => Mathf.Max(1f, hoseLength); set => hoseLength = Mathf.Max(1f, value); }
    public float SecondsToBreak => Mathf.Max(0.1f, secondsToBreak);
    public float StrainRecoveryTime => Mathf.Max(0.1f, strainRecoveryTime);
    public float FullPullSpeed => Mathf.Max(0.1f, fullPullSpeed);

    public static HoseWaterRules ForPlayer(GameObject player)
    {
        foreach (var rules in Active)
            if (rules != null && rules.gameObject.scene == player.scene) return rules;
        return null;
    }

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

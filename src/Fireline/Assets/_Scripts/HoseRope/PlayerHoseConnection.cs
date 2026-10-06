using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One source and rope per player, shared by all equipped nozzle types.</summary>
[DisallowMultipleComponent]
public class PlayerHoseConnection : MonoBehaviour
{
    public HoseRopeSource Source { get; private set; }
    public HoseRope Rope { get; private set; }
    public bool IsConnected => Source != null && Source.IsAvailable && Rope != null;

    public static Color ColorForPlayer(int index)
    {
        switch (Mathf.Max(0, index) % 4)
        {
            case 0: return Color.red;
            case 1: return Color.yellow;
            case 2: return Color.green;
            default: return Color.blue;
        }
    }

    private HoseWeapon CurrentNozzle()
    {
        var loadout = GetComponent<HoseLoadout>();
        return loadout != null ? loadout.Current : GetComponent<HoseWeapon>();
    }

    public bool Attach(HoseRopeSource source)
    {
        var nozzle = CurrentNozzle();
        if (!isActiveAndEnabled || source == null || !source.IsAvailable || nozzle == null
            || source.gameObject.scene != gameObject.scene) return false;
        if (Source == source && IsConnected) return true;
        Detach();
        Source = source;
        Rope = source.CreateRope(this, nozzle.NozzleTransform);
        var input = GetComponent<PlayerInput>();
        Rope.SetColor(ColorForPlayer(input != null ? input.playerIndex : 0));
        return true;
    }

    public void Detach()
    {
        if (Source != null) Source.Release(this);
        Source = null;
        if (Rope != null)
        {
            Rope.gameObject.SetActive(false);
            Destroy(Rope.gameObject);
        }
        Rope = null;
        StopIfDry();
    }

    private void Update()
    {
        if (Source != null && !IsConnected) Detach();
        if (Rope != null)
        {
            var nozzle = CurrentNozzle();
            Rope.SetNozzle(nozzle != null ? nozzle.NozzleTransform : null);
        }
        StopIfDry();
    }

    private void StopIfDry()
    {
        if (HoseWaterRules.CanSpray(gameObject)) return;
        foreach (var nozzle in GetComponents<HoseWeapon>()) nozzle.StopSpraying();
    }

    private void OnDisable() => Detach();
}

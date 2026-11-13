using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The one place that answers "who is this player?": their index, name, color and portrait.
/// Rings, the HUD, hoses and status badges all ask this instead of working it out themselves.
///
/// Put it on the Player prefab and assign the roster. Without a roster (or with an empty
/// profile slot) it falls back to the scene's hose colors and "P1"-style names, as before.
/// </summary>
[DisallowMultipleComponent]
public class PlayerIdentity : MonoBehaviour
{
    [Tooltip("Profiles for P1, P2, P3... Optional.")]
    [SerializeField] private PlayerRoster roster;

    private PlayerInput _input;

    /// <summary>0 for P1, 1 for P2...</summary>
    public int Index
    {
        get
        {
            if (_input == null) _input = GetComponent<PlayerInput>();
            return _input != null ? Mathf.Max(0, _input.playerIndex) : 0;
        }
    }

    /// <summary>This player's profile, or null if none is set up.</summary>
    public PlayerProfile Profile => roster != null ? roster.Get(Index) : null;

    public string DisplayName
    {
        get
        {
            var profile = Profile;
            return profile != null && !string.IsNullOrEmpty(profile.DisplayName) ? profile.DisplayName : $"P{Index + 1}";
        }
    }

    public Color Color
    {
        get
        {
            var profile = Profile;
            if (profile != null) return profile.Color;
            var rules = HoseWaterRules.ForPlayer(gameObject);
            return rules != null ? rules.GetHoseColor(Index) : PlayerHoseConnection.ColorForPlayer(Index);
        }
    }

    /// <summary>The identity on a player, adding one (with no roster) if it's missing.</summary>
    public static PlayerIdentity Of(GameObject player)
    {
        if (player == null) return null;
        var identity = player.GetComponent<PlayerIdentity>();
        return identity != null ? identity : player.AddComponent<PlayerIdentity>();
    }
}

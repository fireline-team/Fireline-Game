using UnityEngine;

/// <summary>
/// The player profiles in join order: first entry is P1, second P2... If more players join
/// than there are entries, it wraps around. Create > Fireline > Player Roster.
/// </summary>
[CreateAssetMenu(fileName = "PlayerRoster", menuName = "Fireline/Player Roster")]
public class PlayerRoster : ScriptableObject
{
    [SerializeField] private PlayerProfile[] players = new PlayerProfile[0];

    /// <summary>The profile for a player index (0 = P1), or null if the roster is empty.</summary>
    public PlayerProfile Get(int playerIndex)
    {
        if (players == null || players.Length == 0) return null;
        return players[Mathf.Max(0, playerIndex) % players.Length];
    }
}

using UnityEngine;

/// <summary>
/// Who one player slot is: name, color and HUD portrait. Make one per player
/// (Create > Fireline > Player Profile) and list them in a PlayerRoster.
/// Everything that marks a player (ground ring, HUD badge, hose, status badge) reads from here.
/// </summary>
[CreateAssetMenu(fileName = "PlayerProfile", menuName = "Fireline/Player Profile")]
public class PlayerProfile : ScriptableObject
{
    [Tooltip("Shown on the HUD, e.g. P1.")]
    [SerializeField] private string displayName = "P1";
    [Tooltip("This player's color: ground ring, HUD outline, hose, status badge.")]
    [SerializeField] private Color color = Color.red;

    [Header("HUD Portrait (optional: empty uses the HUD's default faces)")]
    [Tooltip("Faces from full health to low health, like the HUD's Health Expressions.")]
    [SerializeField] private Sprite[] healthExpressions = new Sprite[0];
    [SerializeField] private Sprite hitExpression;
    [SerializeField] private Sprite downedExpression;

    public string DisplayName => displayName;
    public Color Color => color;
    public Sprite[] HealthExpressions => healthExpressions;
    public Sprite HitExpression => hitExpression;
    public Sprite DownedExpression => downedExpression;
    public bool HasPortrait => (healthExpressions != null && healthExpressions.Length > 0) || hitExpression != null || downedExpression != null;
}

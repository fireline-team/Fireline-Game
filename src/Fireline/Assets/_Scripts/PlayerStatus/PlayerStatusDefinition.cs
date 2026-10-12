using UnityEngine;

[CreateAssetMenu(menuName = "Fireline/Player Status", fileName = "PlayerStatus")]
public class PlayerStatusDefinition : ScriptableObject
{
    [Tooltip("Highest priority wins. Equal priorities use the most recently set status.")]
    public int priority = 100;
    public string label = "!";
    public Sprite icon;
    [Tooltip("Built-in skull drawing; works without a font glyph or external sprite.")]
    public bool useSkull;
    public Color tint = Color.white;
}

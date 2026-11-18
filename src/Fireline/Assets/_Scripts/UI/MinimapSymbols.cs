using UnityEngine;

/// <summary>Pixel glyphs shared by map markers and the objective checklist.</summary>
public static class MinimapSymbols
{
    public static Color32 Objective => new Color32(255, 230, 80, 255);
    public static Color32 Fire => new Color32(255, 161, 40, 255);
    public static Color32 Crate => new Color32(217, 153, 89, 255);
    public static Color32 Civilian => new Color32(178, 230, 255, 255);
    public static Color32 Evac => new Color32(70, 230, 240, 255);

    public static bool Contains(char symbol, int x, int y) =>
        symbol == 'O' ? Mathf.Abs(x) + Mathf.Abs(y) == 2
        : symbol == 'X' ? Mathf.Abs(x) == Mathf.Abs(y)
        : symbol == 'E' ? x == 0 || y == 0
        : symbol == 'F' ? (y <= 0 && Mathf.Abs(x) <= 1) || (y > 0 && x == (y == 2 ? 0 : -1))
        : symbol == 'C' ? (y == 2 && x == 0) || (y == 0 && Mathf.Abs(x) <= 1) || (y == -1 && x == 0) || (y == -2 && Mathf.Abs(x) == 1)
        : symbol == 'D' ? Mathf.Abs(x) == 2 || Mathf.Abs(y) == 2 || x == y
        : Mathf.Abs(x) + Mathf.Abs(y) <= 2;

    public static Texture2D CreateTexture(char symbol, Color32 color)
    {
        var texture = new Texture2D(5, 5, TextureFormat.RGBA32, false)
        { name = "Minimap glyph " + symbol, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[25];
        for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
                if (Contains(symbol, x, y)) pixels[(y + 2) * 5 + x + 2] = color;
        texture.SetPixels32(pixels); texture.Apply(false, false);
        return texture;
    }
}

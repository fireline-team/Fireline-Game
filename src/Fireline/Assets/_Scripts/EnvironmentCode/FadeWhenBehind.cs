using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades a structure see-through while a player is behind it and hidden by it, so players
/// never lose track of themselves behind buildings. Put it on the Structure root
/// (Create Structure adds it automatically). It fades every sprite under this object.
///
/// "Behind" means further up the screen than this object's position (the structure's front
/// edge). "Hidden" means the player's body is inside the structure's area on screen.
/// </summary>
[DisallowMultipleComponent]
public class FadeWhenBehind : MonoBehaviour
{
    [Tooltip("How see-through the structure gets while a player is behind it.")]
    [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.35f;
    [Tooltip("How fast it fades in and out, in alpha per second.")]
    [SerializeField, Min(0.1f)] private float fadeSpeed = 5f;
    [Tooltip("Roughly the height of a player's body center above their feet, used to test if they're hidden.")]
    [SerializeField, Min(0f)] private float playerCheckHeight = 0.6f;

    // Shared by every FadeWhenBehind, so a level full of walls only searches for players once.
    private const string PlayerTag = "Player";
    private static readonly List<Transform> Players = new List<Transform>(4);
    private static float _nextPlayerRefresh;

    private SpriteRenderer[] _renderers;
    private float[] _baseAlphas;
    private float _fade = 1f;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _baseAlphas = new float[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _baseAlphas[i] = _renderers[i].color.a;
    }

    private void OnDisable() => SetFade(1f);

    private void LateUpdate()
    {
        float target = IsHidingAPlayer() ? fadedAlpha : 1f;
        if (Mathf.Approximately(_fade, target)) return;
        SetFade(Mathf.MoveTowards(_fade, target, fadeSpeed * Time.deltaTime));
    }

    private bool IsHidingAPlayer()
    {
        Camera cam = Camera.main;
        if (cam == null || _renderers.Length == 0) return false;
        RefreshPlayers();

        bool haveRect = false;
        Rect screenRect = default;

        for (int i = 0; i < Players.Count; i++)
        {
            Transform player = Players[i];
            if (player == null || !player.gameObject.activeInHierarchy) continue;
            if (player.position.y <= transform.position.y) continue; // in front of us

            if (!haveRect)
            {
                screenRect = ScreenRect(cam);
                haveRect = true;
            }

            Vector3 body = player.position + TiltedView.Up * playerCheckHeight;
            if (screenRect.Contains(cam.WorldToScreenPoint(body))) return true;
        }
        return false;
    }

    // The on-screen rectangle covered by all of this structure's sprites.
    private Rect ScreenRect(Camera cam)
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        foreach (SpriteRenderer r in _renderers)
        {
            if (r == null || !r.enabled || r.sprite == null) continue;
            Bounds b = r.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 p = new Vector3(
                    (corner & 1) == 0 ? b.min.x : b.max.x,
                    (corner & 2) == 0 ? b.min.y : b.max.y,
                    (corner & 4) == 0 ? b.min.z : b.max.z);
                Vector2 s = cam.WorldToScreenPoint(p);
                min = Vector2.Min(min, s);
                max = Vector2.Max(max, s);
            }
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void SetFade(float fade)
    {
        _fade = fade;
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            Color c = _renderers[i].color;
            c.a = _baseAlphas[i] * fade;
            _renderers[i].color = c;
        }
    }

    private static void RefreshPlayers()
    {
        if (Time.time < _nextPlayerRefresh) return;
        _nextPlayerRefresh = Time.time + 0.5f;

        Players.Clear();
        foreach (GameObject go in GameObject.FindGameObjectsWithTag(PlayerTag))
            Players.Add(go.transform);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Players.Clear();
        _nextPlayerRefresh = 0f;
    }
}

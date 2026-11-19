using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades a structure see-through while a player is behind it and hidden by it, so players
/// never lose track of themselves behind buildings. Put it on the Structure root
/// (Create Structure adds it automatically). It fades every sprite under this object.
///
/// A face hides a player when the camera-to-body segment crosses its local sprite bounds.
///
/// On a Structure using depth, the faces are switched to their normal sprite material while
/// faded (a depth material can't be see-through) and back once fully solid again.
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
    private Structure _structure;
    private float[] _baseAlphas;
    private float _fade = 1f;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _structure = GetComponent<Structure>();
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

        foreach (Transform player in Players)
        {
            if (player == null || !player.gameObject.activeInHierarchy) continue;
            Vector3 body = player.position + TiltedView.Up * playerCheckHeight;
            Vector3 viewport = cam.WorldToViewportPoint(body);
            if (viewport.z <= cam.nearClipPlane || viewport.x < 0 || viewport.x > 1 ||
                viewport.y < 0 || viewport.y > 1) continue;

            Vector3 start = cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, cam.nearClipPlane));
            foreach (SpriteRenderer face in _renderers)
            {
                if (face == null || !face.enabled || !face.gameObject.activeInHierarchy || face.sprite == null ||
                    face.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                if (OccludesBody(face, start, body)) return true;
            }
        }
        return false;
    }

    // Test the drawn plane in face space. This includes the roof depth, sprite pivot,
    // perspective and the side faces' shear, without filling the gaps between faces.
    internal static bool OccludesBody(SpriteRenderer face, Vector3 start, Vector3 body)
    {
        Matrix4x4 toLocal = face.transform.worldToLocalMatrix;
        Vector3 from = toLocal.MultiplyPoint3x4(start);
        Vector3 delta = toLocal.MultiplyVector(body - start);
        if (Mathf.Abs(delta.z) < 0.00001f) return false;
        Bounds bounds = face.localBounds;
        float t = (bounds.center.z - from.z) / delta.z;
        if (t < 0f || t >= 1f) return false;
        Vector3 hit = from + delta * t;
        return hit.x >= bounds.min.x && hit.x <= bounds.max.x &&
               hit.y >= bounds.min.y && hit.y <= bounds.max.y;
    }
    private void SetFade(float fade)
    {
        _fade = fade;
        // Swap materials before changing alpha, so the first faded frame is already see-through.
        if (_structure != null) _structure.SetSeeThrough(fade < 0.999f);
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

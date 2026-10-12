using UnityEngine;

/// <summary>
/// Minimal global hit-freeze: drops Time.timeScale to 0 (or a slow-mo value) for a few
/// real-time milliseconds, then restores it. Calls during a freeze extend it to the later
/// end time instead of stacking, so ten hits in one frame don't freeze ten times as long.
///
///   HitStop.Freeze(0.06f);
///
/// Everything that runs on scaled time pauses with it: enemies, physics, the axe sweep, cooldowns.
/// It won't fight a pause menu: if timeScale is already 0 when a freeze starts, it does nothing.
///
/// Swap this out for Feel's MMFreezeFrame (or similar) later without touching the axe; only
/// AxeSwingVisual calls it.
/// </summary>
public static class HitStop
{
    private static bool _active;
    private static float _until;
    private static float _restoreScale = 1f;
    private static Runner _runner;

    public static bool IsFrozen => _active;

    /// <param name="seconds">Real-time length of the freeze.</param>
    /// <param name="timeScale">0 = full stop. Something like 0.05 gives a crunchy slow-mo instead.</param>
    public static void Freeze(float seconds, float timeScale = 0f)
    {
        if (seconds <= 0f) return;

        if (!_active)
        {
            if (Time.timeScale <= 0f) return; // paused by something else; leave it alone
            _restoreScale = Time.timeScale;
            _active = true;
        }

        Time.timeScale = Mathf.Min(Time.timeScale, Mathf.Max(0f, timeScale));
        _until = Mathf.Max(_until, Time.unscaledTime + seconds);
        EnsureRunner();
    }

    /// <summary>Ends any freeze immediately (e.g. when loading a scene).</summary>
    public static void Cancel()
    {
        if (!_active) return;
        _active = false;
        Time.timeScale = _restoreScale;
    }

    private static void EnsureRunner()
    {
        if (_runner != null) return;
        var go = new GameObject("[HitStop]") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        private void Update()
        {
            if (_active && Time.unscaledTime >= _until) Cancel();
        }

        private void OnDestroy() => Cancel();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _active = false;
        _until = 0f;
        _restoreScale = 1f;
        _runner = null;
    }
}

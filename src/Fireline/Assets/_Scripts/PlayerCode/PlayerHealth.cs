 using System;
using System.Collections.Generic;
using Fireline.Shared.Infrastructure;
using Game.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D), typeof(PlayerInput))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [Tooltip("Brief immunity shared across all touching enemies and fire zones, in seconds.")]
    [SerializeField, Min(0.05f)] private float contactDamageCooldown = 0.75f;

    [Header("Revival")]
    [SerializeField, Min(1f)] private float waterToRevive = 90f;
    [SerializeField, Range(1f, 100f)] private float revivedHealthPercent = 50f;
    [SerializeField, Min(0f)] private float reviveInvulnerabilitySeconds = 2f;
    private float revivalWater;
    private static readonly List<PlayerHealth> players = new List<PlayerHealth>();
    public static IReadOnlyList<PlayerHealth> ActivePlayers => players;
    public bool IsDowned => IsDead;
    public float ReviveProgress => IsDowned ? Mathf.Clamp01(revivalWater / Mathf.Max(1f, waterToRevive)) : 0f;
    public event Action Revived;
    public bool CanResetScene
    {
        get
        {
            if (!IsDowned) return false;
            foreach (var player in players)
                if (player != null && player.gameObject.scene == gameObject.scene && !player.IsDowned) return false;
            return true;
        }
    }

    private void OnEnable() => players.Add(this);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearPlayers() => players.Clear();

    [Header("Damage Feedback")]
    [Tooltip("Blinks per second while invulnerable after a hit.")]
    [SerializeField, Min(1f)] private float blinkRate = 10f;
    [Tooltip("How see-through the player gets on each blink. 0 = invisible, 1 = no blink.")]
    [SerializeField, Range(0f, 1f)] private float blinkAlpha = 0.3f;

    private HealthPool health;
    private Rigidbody2D body;
    private PlayerInput input;
    private float nextContactTime;
    private bool resetting;
    private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
    private SpriteRenderer[] sprites;
    private float[] spriteAlphas;
    private bool blinkedOut;

    public float CurrentHealth => health.Current;
    public float MaxHealth => health.Maximum;
    public bool IsDead => health != null && health.IsDead;
    /// <summary>True during the brief immunity after a hit (the "i-frames").</summary>
    public bool IsInvulnerable => !IsDead && Time.time < nextContactTime;
    public event Action Died;

    private void Awake()
    {
        health = new HealthPool(maxHealth);
        body = GetComponent<Rigidbody2D>();
        input = GetComponent<PlayerInput>();

        // Every sprite on the player (body, hands, hose) blinks together.
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        spriteAlphas = new float[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            spriteAlphas[i] = sprites[i].color.a;
    }

    private void Update()
    {
        bool fadeNow = false;
        if (IsInvulnerable)
        {
            // Alternate between faded and solid, starting faded right after the hit.
            float elapsed = contactDamageCooldown - (nextContactTime - Time.time);
            fadeNow = Mathf.FloorToInt(elapsed * blinkRate * 2f) % 2 == 0;
        }
        if (fadeNow != blinkedOut) SetBlink(fadeNow);
    }

    private void SetBlink(bool faded)
    {
        blinkedOut = faded;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            Color c = sprites[i].color;
            c.a = faded ? spriteAlphas[i] * blinkAlpha : spriteAlphas[i];
            sprites[i].color = c;
        }
    }

    public void TakeDamage(float amount)
    {
        if (!health.TakeDamage(amount)) return;
        revivalWater = 0f;
        if (blinkedOut) SetBlink(false);
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        // A downed player must not be pushed around by other players.
        body.simulated = false;
        foreach (var hose in GetComponents<HoseWeapon>()) hose.StopSpraying();
        Died?.Invoke();
    }

    public void ApplyRevivalWater(PlayerHealth teammate, float amount)
    {
        if (!isActiveAndEnabled || !IsDowned || teammate == null || teammate == this
            || !teammate.isActiveAndEnabled || teammate.IsDowned || teammate.gameObject.scene != gameObject.scene
            || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        revivalWater = Mathf.Min(Mathf.Max(1f, waterToRevive), revivalWater + amount);
        if (ReviveProgress < 1f) return;
        float maximum = health.Maximum;
        health.Reset(maximum);
        health.TakeDamage(maximum * (1f - Mathf.Clamp(revivedHealthPercent, 1f, 100f) / 100f));
        revivalWater = 0f;
        resetting = false;
        contacts.Clear();
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = true;
        nextContactTime = Time.time + Mathf.Max(0f, reviveInvulnerabilitySeconds);
        Revived?.Invoke();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        contacts.Add(other);
        TryContactDamage(other);
    }

    private void OnTriggerExit2D(Collider2D other) => contacts.Remove(other);

    private void FixedUpdate()
    {
        if (IsDead || Time.time < nextContactTime) return;
        // TriggerStay is not delivered for sleeping rigidbodies. Keep contact damage
        // running even when a player stands still, and prune despawned enemies.
        contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        foreach (Collider2D contact in contacts)
        {
            TryContactDamage(contact);
            if (IsDead || Time.time < nextContactTime) break;
        }
    }

    private void OnDisable()
    {
        players.Remove(this);
        contacts.Clear();
        if (blinkedOut) SetBlink(false);
    }

    private void TryContactDamage(Collider2D other)
    {
        if (IsDead || Time.time < nextContactTime) return;
        HordeEnemy enemy = other.GetComponentInParent<HordeEnemy>();
        float damage = 0f;
        if (enemy != null && enemy.isActiveAndEnabled && enemy.CurrentHealth > 0f)
            damage = enemy.ContactDamage;
        FireZone fire = other.GetComponentInParent<FireZone>();
        if (fire != null && fire.isActiveAndEnabled)
            damage = Mathf.Max(damage, fire.ContactDamage);
        if (damage <= 0f) return;
        nextContactTime = Time.time + contactDamageCooldown;
        TakeDamage(damage);
    }

    public void ResetScene()
    {
        if (!CanResetScene || resetting) return;
        Scene scene = gameObject.scene;
        resetting = true;
#if UNITY_EDITOR
        // Also support prototype scenes opened directly, outside the build list.
        if (scene.buildIndex < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(scene.path);
    }
    /* 
    private void OnGUI()
    {
        int index = Mathf.Max(0, input.playerIndex);
        float width = Mathf.Min(300f, Screen.width);
        GUILayout.BeginArea(new Rect(Screen.width - width - 10f, 10f + index * 120f, width, 115f), GUI.skin.box);
        GUILayout.Label($"Player {index + 1} — Health {Mathf.CeilToInt(CurrentHealth)} / {MaxHealth:0}");
        if (IsDead)
        {
            if (!CanResetScene)
            {
                GUILayout.Label($"Downed — teammates: spray to revive ({ReviveProgress:P0}).");
                GUILayout.EndArea();
                return;
            }
            GUILayout.Label("Everyone is down. Reset restarts the level.");
            string binding = input.currentControlScheme == "KeyboardMouse" ? "E" : "south face button";
            GUILayout.Label($"Press {binding} or click below.");
            if (GUILayout.Button("Reset level")) ResetScene();
        }
        GUILayout.EndArea();
    }
    */
}

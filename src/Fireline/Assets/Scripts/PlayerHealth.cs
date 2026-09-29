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
    [Tooltip("Brief immunity shared across all touching enemies, in seconds.")]
    [SerializeField, Min(0.05f)] private float contactDamageCooldown = 0.75f;

    private HealthPool health;
    private Rigidbody2D body;
    private PlayerInput input;
    private float nextContactTime;
    private bool resetting;
    private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();

    public float CurrentHealth => health.Current;
    public float MaxHealth => health.Maximum;
    public bool IsDead => health != null && health.IsDead;
    public event Action Died;

    private void Awake()
    {
        health = new HealthPool(maxHealth);
        body = GetComponent<Rigidbody2D>();
        input = GetComponent<PlayerInput>();
    }

    public void TakeDamage(float amount)
    {
        if (!health.TakeDamage(amount)) return;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        // A downed player must not be pushed around by other players.
        body.simulated = false;
        Died?.Invoke();
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

    private void OnDisable() => contacts.Clear();

    private void TryContactDamage(Collider2D other)
    {
        if (IsDead || Time.time < nextContactTime) return;
        HordeEnemy enemy = other.GetComponentInParent<HordeEnemy>();
        if (enemy == null || !enemy.isActiveAndEnabled || enemy.CurrentHealth <= 0f
            || enemy.ContactDamage <= 0f) return;
        nextContactTime = Time.time + contactDamageCooldown;
        TakeDamage(enemy.ContactDamage);
    }

    public void ResetScene()
    {
        if (!IsDead || resetting) return;
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

    private void OnGUI()
    {
        int index = Mathf.Max(0, input.playerIndex);
        float width = Mathf.Min(300f, Screen.width);
        GUILayout.BeginArea(new Rect(Screen.width - width - 10f, 10f + index * 120f, width, 115f), GUI.skin.box);
        GUILayout.Label($"Player {index + 1} — Health {Mathf.CeilToInt(CurrentHealth)} / {MaxHealth:0}");
        if (IsDead)
        {
            GUILayout.Label("You are down. Reset restarts the level for everyone.");
            string binding = input.currentControlScheme == "KeyboardMouse" ? "E" : "south face button";
            GUILayout.Label($"Press {binding} or click below.");
            if (GUILayout.Button("Reset level")) ResetScene();
        }
        GUILayout.EndArea();
    }
}

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public class CombatRollTests
{
    private PlayerInput player;
    private Gamepad gamepad;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        gamepad = InputSystem.AddDevice<Gamepad>();
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab");
        player = PlayerInput.Instantiate(prefab, controlScheme: "Gamepad", pairWithDevice: gamepad);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Object.Destroy(player.gameObject);
        InputSystem.RemoveDevice(gamepad);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RollLocksDirectionProtectsHealthAndRestoresMaterials()
    {
        var controller = player.GetComponent<PlayerController>();
        var health = player.GetComponent<PlayerHealth>();
        var sprites = player.GetComponentsInChildren<SpriteRenderer>(true);
        var materials = sprites.Select(s => s.sharedMaterial).ToArray();
        int starts = 0, ends = 0, ready = 0;
        float remaining = -1;
        controller.RollStarted += () => starts++;
        controller.RollEnded += () => ends++;
        controller.RollReady += () => ready++;
        controller.RollCooldownChanged += (value, total) => remaining = value;
        typeof(PlayerHealth).GetField("contactDamageCooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(health, 0.05f);
        health.TakeEnemyDamage(30);
        Assert.That(health.IsBurning, Is.True);
        Assert.That(controller.TryCombatRoll(Vector2.zero), Is.False);
        Assert.That(health.IsBurning, Is.True, "Rejected rolls cannot extinguish.");
        Assert.That(controller.TryCombatRoll(Vector2.right), Is.True);
        Assert.That(health.IsBurning, Is.False);
        Assert.That(controller.TryCombatRoll(Vector2.up), Is.False);
        Assert.That(health.IsInvulnerable, Is.True);
        float before = health.CurrentHealth;
        health.TakeDamage(1000);
        Assert.That(health.CurrentHealth, Is.EqualTo(before));
        Assert.That(sprites.All(s => s.sharedMaterial == Resources.Load<Material>("CombatRollFlash")), Is.True);
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
        yield return null;
        yield return new WaitForFixedUpdate();
        Assert.That(player.GetComponent<Rigidbody2D>().linearVelocity.y, Is.EqualTo(0).Within(0.001f));
        yield return new WaitForSeconds(0.3f);
        Assert.That(controller.IsRolling, Is.False);
        Assert.That(health.IsInvulnerable, Is.True, "Immunity must outlast dash movement.");
        health.TakeDamage(1000);
        Assert.That(health.CurrentHealth, Is.EqualTo(before));
        Assert.That(sprites.All(s => s.sharedMaterial == Resources.Load<Material>("CombatRollFlash")), Is.True);
        Assert.That(controller.TryCombatRoll(Vector2.right), Is.False);
        Assert.That(remaining, Is.GreaterThan(0));
        yield return new WaitForSeconds(0.25f);
        Assert.That(health.IsInvulnerable, Is.False);
        health.TakeDamage(1);
        Assert.That(health.CurrentHealth, Is.EqualTo(before - 1));
        for (int i = 0; i < sprites.Length; i++) Assert.That(sprites[i].sharedMaterial, Is.SameAs(materials[i]));
        yield return new WaitForSeconds(1.6f);
        Assert.That(starts, Is.EqualTo(1));
        Assert.That(ends, Is.EqualTo(1));
        Assert.That(ready, Is.EqualTo(1));
        Assert.That(remaining, Is.Zero);
        Assert.That(controller.TryCombatRoll(Vector2.up), Is.True);
        controller.enabled = false;
        Assert.That(health.IsInvulnerable, Is.False);
        Assert.That(controller.IsRolling, Is.False);
    }

    [UnityTest]
    public IEnumerator LeftBumperStartsRollAndSpaceIsBound()
    {
        var action = player.actions["CombatRoll"];
        Assert.That(action.bindings.Any(b => b.path == "<Keyboard>/space"), Is.True);
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.right }
            .WithButton(GamepadButton.LeftShoulder));
        yield return null;
        yield return null;
        Assert.That(player.GetComponent<PlayerController>().IsRolling, Is.True);
    }
}
#endif

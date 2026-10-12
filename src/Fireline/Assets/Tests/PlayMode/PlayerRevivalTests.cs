using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class PlayerRevivalTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private readonly List<InputDevice> devices = new List<InputDevice>();
    private IEnumerator Load()
    {
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/_Scenes/Isometric HoseRope Jake.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        Object.FindAnyObjectByType<PlayerInputManager>().DisableJoining();
        var rules = Object.FindAnyObjectByType<HoseWaterRules>();
        rules.RequireWaterSource = false;
        rules.LimitHoseLength = false;
    }
    private PlayerInput Join(int index, Gamepad pad = null)
    {
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab");
        var player = pad == null ? PlayerInput.Instantiate(prefab, playerIndex: index)
            : PlayerInput.Instantiate(prefab, playerIndex: index, controlScheme: "Gamepad", pairWithDevice: pad);
        created.Add(player.gameObject);
        player.transform.position = new Vector3(100,100,0);
        if (pad == null) player.GetComponent<PlayerController>().enabled = false;
        return player;
#else
        return null;
#endif
    }
    private static void Set(PlayerHealth player, string field, float value) =>
        typeof(PlayerHealth).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, value);
    private static void PlaceTarget(PlayerInput source, PlayerInput target)
    {
        var nozzle = source.GetComponent<HoseLoadout>().Current;
        Vector3 ground = TiltedView.ToGround(nozzle.NozzleTransform.position, source.transform.position.z);
        target.transform.position = ground + Vector3.right;
        Physics2D.SyncTransforms();
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var obj in created) if (obj != null) Object.Destroy(obj);
        created.Clear();
        foreach (var device in devices) InputSystem.RemoveDevice(device);
        devices.Clear();
        var scene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Revival cleanup"));
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator CooperativeWaterRevivesAtConfiguredHealthAndClearsBurningStatus()
    {
        yield return Load();
        var first = Join(0); var second = Join(1); var target = Join(2);
        first.GetComponent<Rigidbody2D>().simulated = second.GetComponent<Rigidbody2D>().simulated = false;
        var health = target.GetComponent<PlayerHealth>();
        Set(health, "waterToRevive", 90); Set(health, "revivedHealthPercent", 35);
        health.TakeDamage(10000);
        PlaceTarget(first,target);
        int revived = 0; health.Revived += () => revived++;
        Assert.That(health.CanResetScene, Is.False);
        Assert.That(target.GetComponent<PlayerDownedFire>().IsBurningVisible, Is.True);
        var hose = first.GetComponent<HoseLoadout>().Current;
        hose.Spray(Vector2.right, 0.5f);
        float oneContribution = health.ReviveProgress;
        Assert.That(oneContribution, Is.GreaterThan(0));
        second.GetComponent<HoseLoadout>().Current.Spray(Vector2.right, 0.5f);
        Assert.That(health.ReviveProgress, Is.EqualTo(oneContribution * 2).Within(0.001f));
        float partial = health.ReviveProgress;
        yield return null; yield return null;
        Assert.That(health.ReviveProgress, Is.EqualTo(partial));
        Assert.That(target.GetComponent<PlayerStatusMarker>().CurrentProgress, Is.EqualTo(partial));
        hose.Spray(Vector2.right, 10);
        Assert.That(health.IsDowned, Is.False);
        Assert.That(health.CurrentHealth, Is.EqualTo(35));
        Assert.That(health.IsInvulnerable, Is.True);
        Assert.That(target.GetComponent<Rigidbody2D>().simulated, Is.True);
        Assert.That(target.GetComponent<PlayerDownedFire>().IsBurningVisible, Is.False);
        Assert.That(target.GetComponent<PlayerStatusMarker>().CurrentStatus, Is.Null);
        hose.Spray(Vector2.right, 10);
        Assert.That(revived, Is.EqualTo(1));
        health.TakeDamage(10000);
        Assert.That(health.ReviveProgress, Is.Zero);
        Assert.That(target.GetComponent<PlayerDownedFire>().IsBurningVisible, Is.True);
    }

    [UnityTest]
    public IEnumerator EveryNozzleRevivesButWallsDrySourcesAndDownedShootersDoNot()
    {
        yield return Load();
        var shooter = Join(0); var target = Join(1);
        shooter.GetComponent<Rigidbody2D>().simulated = false;
        var health = target.GetComponent<PlayerHealth>();
        var loadout = shooter.GetComponent<HoseLoadout>();
        Set(health, "waterToRevive", 1);
        foreach (var type in new[] { NozzleType.Standard, NozzleType.Mist, NozzleType.Jet })
        {
            loadout.Equip(type); health.TakeDamage(10000); PlaceTarget(shooter,target);
            loadout.Current.Spray(Vector2.right,1);
            Assert.That(health.IsDowned, Is.False, type.ToString());
        }
        loadout.Equip(NozzleType.Standard); health.TakeDamage(10000); PlaceTarget(shooter,target);
        var wall = new GameObject("Revival blocker"); created.Add(wall);
        wall.layer = LayerMask.NameToLayer("Obstacles");
        wall.transform.position = target.transform.position - Vector3.right * 0.5f;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(0.1f,3);
        Physics2D.SyncTransforms();
        loadout.Current.Spray(Vector2.right,10);
        Assert.That(health.ReviveProgress, Is.Zero);
        Object.Destroy(wall); yield return null;
        var rules = Object.FindAnyObjectByType<HoseWaterRules>(); rules.RequireWaterSource = true;
        shooter.GetComponent<PlayerHoseConnection>().Detach();
        loadout.Current.Spray(Vector2.right,10);
        Assert.That(health.ReviveProgress, Is.Zero);
        rules.RequireWaterSource = false;
        shooter.GetComponent<PlayerHealth>().TakeDamage(10000);
        loadout.Current.Spray(Vector2.right,10);
        health.ApplyRevivalWater(health,1000);
        health.ApplyRevivalWater(shooter.GetComponent<PlayerHealth>(),1000);
        Assert.That(health.ReviveProgress, Is.Zero);
        Assert.That(health.CanResetScene, Is.True);
    }

    [UnityTest]
    public IEnumerator DownedControlsAreBlockedAndMovementAndAttacksReturnAfterRevival()
    {
        yield return Load();
        var helper = Join(0);
        helper.GetComponent<Rigidbody2D>().simulated = false;
        var pad = InputSystem.AddDevice<Gamepad>(); devices.Add(pad);
        var target = Join(1,pad);
        var health = target.GetComponent<PlayerHealth>();
        health.TakeDamage(10000);
        var position = target.transform.position;
        InputSystem.QueueStateEvent(pad,new GamepadState { leftStick=Vector2.right, rightStick=Vector2.right, rightTrigger=1 }.WithButton(GamepadButton.South));
        yield return null; yield return new WaitForFixedUpdate(); yield return null;
        Assert.That(target.transform.position, Is.EqualTo(position));
        Assert.That(target.GetComponent<HoseLoadout>().Current.IsStreamActive, Is.False);
        Assert.That(target.GetComponent<AxeSwing>().TrySwing(), Is.False);
        health.ApplyRevivalWater(helper.GetComponent<PlayerHealth>(),float.NaN);
        Assert.That(health.ReviveProgress, Is.Zero);
        health.ApplyRevivalWater(helper.GetComponent<PlayerHealth>(),1000);
        yield return null; yield return new WaitForFixedUpdate(); yield return null;
        Assert.That(target.GetComponent<Rigidbody2D>().linearVelocity.x, Is.GreaterThan(0));
        Assert.That(target.GetComponent<HoseLoadout>().Current.IsStreamActive, Is.True);
        Assert.That(target.GetComponent<AxeSwing>().TrySwing(), Is.True);
    }
}

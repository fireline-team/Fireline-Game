using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class HoseConnectionTests
{
    private readonly List<GameObject> players = new List<GameObject>();
    private readonly List<InputDevice> devices = new List<InputDevice>();

    private IEnumerator LoadScene()
    {
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/_Scenes/Isometric HoseRope Jake.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        Object.FindAnyObjectByType<PlayerInputManager>().DisableJoining();
    }

    private PlayerInput Join(int index, InputDevice device = null)
    {
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab");
        var player = device == null ? PlayerInput.Instantiate(prefab, playerIndex: index)
            : PlayerInput.Instantiate(prefab, playerIndex: index, controlScheme: "Gamepad", pairWithDevice: device);
        players.Add(player.gameObject);
        return player;
#else
        return null;
#endif
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var player in players) if (player != null) Object.Destroy(player);
        players.Clear();
        foreach (var device in devices) InputSystem.RemoveDevice(device);
        devices.Clear();
        // Avoid leaking source-dependent rules into unrelated tests.
        var scene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Hose test cleanup"));
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator InteractionDetachesStaysDetachedAndTransfersOnlyInReach()
    {
        yield return LoadScene();
        var player = Join(0);
        player.GetComponent<PlayerController>().enabled = false;
        yield return null;
        yield return null;
        var connection = player.GetComponent<PlayerHoseConnection>();
        var original = connection.Source;
        var interactor = player.GetComponent<PlayerInteractor>();
        player.transform.position = original.transform.position;
        Assert.That(interactor.TryInteract(), Is.True);
        Assert.That(connection.IsConnected, Is.False);
        for (int i = 0; i < 5; i++) yield return null;
        Assert.That(connection.IsConnected, Is.False, "The source must not automatically reattach.");
        Assert.That(original.ConnectedPlayers, Is.Zero);
        Assert.That(interactor.TryInteract(), Is.True);
        Assert.That(connection.Source, Is.EqualTo(original));
        var sources = Object.FindObjectsByType<HoseRopeSource>(FindObjectsSortMode.None);
        var other = sources[0] == original ? sources[1] : sources[0];
        other.Interact(interactor);
        Assert.That(connection.Source, Is.EqualTo(original), "Distant sources cannot be used.");
        player.transform.position = other.transform.position;
        Assert.That(interactor.TryInteract(), Is.True);
        Assert.That(connection.Source, Is.EqualTo(other));
        Assert.That(original.ConnectedPlayers, Is.Zero);
        Assert.That(other.ConnectedPlayers, Is.EqualTo(1));
        yield return null;
        Assert.That(player.GetComponentsInChildren<HoseRope>().Length, Is.EqualTo(1));
        other.gameObject.SetActive(false);
        Assert.That(connection.IsConnected, Is.False);
        Assert.That(HoseWaterRules.CanSpray(player.gameObject), Is.False);
    }

    [UnityTest]
    public IEnumerator MeleeButtonDetachesAndLegacyToggleRestoresEveryNozzle()
    {
        yield return LoadScene();
        var gamepad = InputSystem.AddDevice<Gamepad>();
        devices.Add(gamepad);
        var player = Join(0, gamepad);
        yield return null;
        yield return null;
        var connection = player.GetComponent<PlayerHoseConnection>();
        var source = connection.Source;
        player.GetComponent<Rigidbody2D>().position = source.transform.position;
        InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1, rightStick = Vector2.right });
        yield return null;
        yield return new WaitForFixedUpdate();
        yield return null;
        var loadout = player.GetComponent<HoseLoadout>();
        Assert.That(loadout.Current.IsStreamActive, Is.True);
        InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 }.WithButton(GamepadButton.South));
        yield return null;
        yield return null;
        Assert.That(connection.IsConnected, Is.False);
        Assert.That(loadout.Current.IsStreamActive, Is.False);
        player.GetComponent<PlayerController>().enabled = false;
        var rules = Object.FindAnyObjectByType<HoseWaterRules>();
        foreach (var type in new[] { NozzleType.Standard, NozzleType.Mist, NozzleType.Jet })
        {
            loadout.Equip(type);
            loadout.Current.Spray(Vector2.right, 0.02f);
            Assert.That(loadout.Current.IsStreamActive, Is.False);
            if (type == NozzleType.Jet) Assert.That(((JetHose)loadout.Current).IsReady, Is.True);
            rules.RequireWaterSource = false;
            loadout.Current.Spray(Vector2.right, 0.02f);
            if (type == NozzleType.Jet) Assert.That(((JetHose)loadout.Current).IsReady, Is.False);
            else Assert.That(loadout.Current.IsStreamActive, Is.True);
            rules.RequireWaterSource = true;
            yield return null;
            yield return null;
            Assert.That(loadout.Current.IsStreamActive, Is.False);
        }
        connection.Attach(source);
        loadout.Equip(NozzleType.Standard);
        loadout.Current.Spray(Vector2.right, 0.02f);
        Assert.That(loadout.Current.IsStreamActive, Is.True);
    }

    [UnityTest]
    public IEnumerator FourPlayersHaveIndependentConnectionsAndPersistentColors()
    {
        yield return LoadScene();
        for (int i = 0; i < 4; i++) Join(i).GetComponent<PlayerController>().enabled = false;
        yield return null;
        yield return null;
        var expected = new[] { Color.red, Color.yellow, Color.green, Color.blue };
        var sources = Object.FindObjectsByType<HoseRopeSource>(FindObjectsSortMode.None);
        for (int i = 0; i < 4; i++)
        {
            var connection = players[i].GetComponent<PlayerHoseConnection>();
            var block = new MaterialPropertyBlock();
            connection.Rope.GetComponent<LineRenderer>().GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(expected[i]));
            connection.Detach();
            connection.Attach(sources[0]);
            connection.Rope.GetComponent<LineRenderer>().GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(expected[i]));
            for (int other = i + 1; other < 4; other++)
                Assert.That(players[other].GetComponent<PlayerHoseConnection>().IsConnected, Is.True);
        }
        Assert.That(sources[0].ConnectedPlayers, Is.EqualTo(4));
    }
}

using System.Collections;
using System.Collections.Generic;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class HealthCombatTests
{
    private readonly List<Object> created = new List<Object>();
    private readonly List<InputDevice> devices = new List<InputDevice>();

    private GameObject NewObject(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        created.Add(go);
        return go;
    }

    private HordeEnemy Enemy(Vector2 position)
    {
        var go = NewObject("Test enemy", position);
        go.SetActive(false);
        go.AddComponent<CircleCollider2D>().isTrigger = true;
        var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        created.Add(definition);
        var enemy = go.AddComponent<HordeEnemy>();
        enemy.SetDefinition(definition);
        go.SetActive(true);
        return enemy;
    }

    private PlayerHealth Player(Vector2 position)
    {
        var go = NewObject("Test player", position);
        go.AddComponent<PlayerInput>();
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0;
        go.AddComponent<BoxCollider2D>();
        return go.AddComponent<PlayerHealth>();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var obj in created)
            if (obj != null) Object.Destroy(obj);
        created.Clear();
        foreach (var device in devices) InputSystem.RemoveDevice(device);
        devices.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator ContactDamagesImmediatelyThenRepeatsAfterCooldown()
    {
        PlayerHealth player = Player(new Vector2(100, 100));
        Enemy(new Vector2(100, 100));
        Enemy(new Vector2(100, 100));
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(player.CurrentHealth, Is.EqualTo(90), "Two enemies must share one contact cooldown.");
        yield return new WaitForSeconds(0.1f);
        Assert.That(player.CurrentHealth, Is.EqualTo(90));
        yield return new WaitForSeconds(0.75f);
        Assert.That(player.CurrentHealth, Is.EqualTo(80));
        player.GetComponent<Rigidbody2D>().position = new Vector2(110, 110);
        yield return new WaitForSeconds(0.8f);
        Assert.That(player.CurrentHealth, Is.EqualTo(80), "Leaving contact must stop damage.");
    }

    [UnityTest]
    public IEnumerator DeathStopsBodyAndOnlyNotifiesOnce()
    {
        PlayerHealth player = Player(new Vector2(100, 100));
        var body = player.GetComponent<Rigidbody2D>();
        body.linearVelocity = Vector2.right * 5;
        int deaths = 0;
        player.Died += () => deaths++;
        player.TakeDamage(1000);
        player.TakeDamage(1000);
        Vector3 position = player.transform.position;
        yield return new WaitForFixedUpdate();
        Assert.That(deaths, Is.EqualTo(1));
        Assert.That(player.CurrentHealth, Is.Zero);
        Assert.That(body.simulated, Is.False);
        Assert.That(player.transform.position, Is.EqualTo(position));
    }

    [UnityTest]
    public IEnumerator HoseOnlyHitsItsAreaAndDamagesEachEnemyOnce()
    {
        PlayerHealth player = Player(new Vector2(100, 100));
        HoseWeapon hose = player.gameObject.AddComponent<HoseWeapon>();
        HordeEnemy hit = Enemy(new Vector2(102, 100));
        hit.gameObject.AddComponent<BoxCollider2D>().isTrigger = true;
        HordeEnemy behind = Enemy(new Vector2(98, 100));
        HordeEnemy side = Enemy(new Vector2(102, 102));
        HordeEnemy far = Enemy(new Vector2(107, 100));
        hose.Spray(Vector2.right, 0.1f);
        Assert.That(hit.CurrentHealth, Is.EqualTo(8).Within(0.001f));
        Assert.That(behind.CurrentHealth, Is.EqualTo(10));
        Assert.That(side.CurrentHealth, Is.EqualTo(10));
        Assert.That(far.CurrentHealth, Is.EqualTo(10));
        player.TakeDamage(100);
        hose.Spray(Vector2.right, 0.1f);
        Assert.That(hit.CurrentHealth, Is.EqualTo(8).Within(0.001f), "Dead players cannot spray.");
        hit.TakeDamage(100);
        yield return null;
        Assert.That(hit == null, Is.True, "Unpooled enemies are destroyed.");
    }

    [UnityTest]
    public IEnumerator PooledEnemyDiesOnceAndRespawnsAtFullHealth()
    {
        HordeEnemy enemy = Enemy(new Vector2(100, 100));
        enemy.gameObject.SetActive(false);
        int releases = 0;
        using (var pool = new ObjectPool<HordeEnemy>(() => enemy,
            e => e.gameObject.SetActive(true), e => { releases++; e.gameObject.SetActive(false); }))
        {
            enemy.Pool = pool;
            pool.Get();
            enemy.TakeDamage(100);
            enemy.TakeDamage(100);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(enemy.gameObject.activeSelf, Is.False);
            var respawned = pool.Get();
            Assert.That(respawned.CurrentHealth, Is.EqualTo(10));
            Assert.That(respawned.gameObject.activeSelf, Is.True);
        }
        yield return null;
    }

#if UNITY_EDITOR
    [UnityTest]
    public IEnumerator PlayerPrefabStopsAllActionsOnDeathAndControllerCanResetScene()
    {
        yield return SceneManager.LoadSceneAsync("Assets/_Scenes/BasicScene.unity");
        Object.FindAnyObjectByType<PlayerInputManager>().DisableJoining();
        var gamepad = InputSystem.AddDevice<Gamepad>();
        devices.Add(gamepad);
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab");
        PlayerInput input = PlayerInput.Instantiate(prefab, controlScheme: "Gamepad", pairWithDevice: gamepad);
        created.Add(input.gameObject);
        var player = input.GetComponent<PlayerHealth>();
        var controller = input.GetComponent<PlayerController>();
        var stream = input.GetComponentInChildren<LineRenderer>();
        InputSystem.QueueStateEvent(gamepad, new GamepadState
        {
            leftStick = Vector2.right, rightStick = Vector2.right, rightTrigger = 1
        });
        yield return null;
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(input.GetComponent<Rigidbody2D>().linearVelocity.x, Is.GreaterThan(0));
        Assert.That(stream.enabled, Is.True);
        player.TakeDamage(100);
        Vector3 deathPosition = player.transform.position;
        Vector2 deathAim = controller.AimDirection;
        InputSystem.QueueStateEvent(gamepad, new GamepadState
        {
            leftStick = Vector2.up, rightStick = Vector2.up, rightTrigger = 1
        });
        yield return new WaitForSeconds(0.4f);
        Assert.That(player.transform.position, Is.EqualTo(deathPosition));
        Assert.That(controller.AimDirection, Is.EqualTo(deathAim));
        Assert.That(stream.enabled, Is.False);
        var oldSceneHandle = SceneManager.GetActiveScene().handle;
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
        for (int frame = 0; frame < 120 && SceneManager.GetActiveScene().handle == oldSceneHandle; frame++)
            yield return null;
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo("Assets/_Scenes/BasicScene.unity"));
        Assert.That(SceneManager.GetActiveScene().handle, Is.Not.EqualTo(oldSceneHandle));
        Assert.That(player == null, Is.True);
    }
#endif
}

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SharedCameraTests
{
    private readonly List<GameObject> players = new List<GameObject>();
    private readonly List<InputDevice> devices = new List<InputDevice>();
    private IEnumerator Load()
    {
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/_Scenes/Isometric HoseRope Jake.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        Object.FindAnyObjectByType<PlayerInputManager>().DisableJoining();
    }
    private PlayerHealth Join(int index)
    {
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab");
        var player = PlayerInput.Instantiate(prefab, playerIndex: index);
        players.Add(player.gameObject);
        player.GetComponent<PlayerController>().enabled = false;
        return player.GetComponent<PlayerHealth>();
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
        var scene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Camera cleanup"));
        yield return SceneManager.UnloadSceneAsync(scene);
    }
    private static Rect BodyScreenBounds(SpriteRenderer body, Camera camera, Vector2 translation)
    {
        Bounds bounds = body.localBounds;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var local in new[] { new Vector3(bounds.min.x, bounds.min.y), new Vector3(bounds.min.x, bounds.max.y),
            new Vector3(bounds.max.x, bounds.min.y), new Vector3(bounds.max.x, bounds.max.y) })
        {
            Vector2 screen = camera.WorldToScreenPoint(body.transform.TransformPoint(local) + (Vector3)translation);
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    [UnityTest]
    public IEnumerator HoseBillboardKeepsTiltAndAimWithoutMovingItsOrbitOrMuzzle()
    {
        yield return Load();
        var player = Join(0);
        Transform pivot = player.transform.Find("AimPivot");
        Transform hose = pivot.Find("Hose");
        Assert.That(hose.GetComponent<AimedBillboard>(), Is.Not.Null);
        Vector3 normal = TiltedView.StandingRotation * Vector3.forward;
        Vector3 localPosition = hose.localPosition;
        foreach (float angle in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
        {
            pivot.rotation = Quaternion.Euler(0, 0, angle);
            Vector3 orbitPosition = hose.position;
            Vector3 muzzlePosition = player.GetComponent<HoseLoadout>().Current.NozzleTransform.position;
            yield return null;
            Assert.That(Vector3.Dot(hose.forward, normal), Is.GreaterThan(0.9999f));
            Vector3 projectedAim = Vector3.ProjectOnPlane(pivot.right, normal).normalized;
            Assert.That(Vector3.Dot(hose.right, projectedAim), Is.GreaterThan(0.9999f));
            Assert.That(Vector3.Distance(hose.position, orbitPosition), Is.LessThan(0.0001f));
            Assert.That(hose.localPosition, Is.EqualTo(localPosition));
            Assert.That(Vector3.Distance(player.GetComponent<HoseLoadout>().Current.NozzleTransform.position,
                muzzlePosition), Is.LessThan(0.0001f));
        }
    }

    [UnityTest]
    public IEnumerator MovingPlayersStayVisibleAtZoomCapAndCameraZoomsInWhenTheyRegroup()
    {
        yield return Load();
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab");
        for (int i = 0; i < 4; i++)
        {
            var pad = InputSystem.AddDevice<Gamepad>(); devices.Add(pad);
            var player = PlayerInput.Instantiate(prefab, playerIndex: i, controlScheme: "Gamepad", pairWithDevice: pad);
            players.Add(player.gameObject);
            // Keep this camera test independent of map obstacle geometry and enemies.
            player.transform.position = new Vector3(i % 2 == 0 ? -1 : 1, 100 + (i < 2 ? -1 : 1), 0);
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(i % 2 == 0 ? -1 : 1, 0) });
        }
        yield return new WaitForSeconds(7);
        var camera = Camera.main;
        float zoomCap = Object.FindAnyObjectByType<CinemachineGroupFraming>().FovRange.y;
        Assert.That(camera.orthographic, Is.False);
        Assert.That(camera.fieldOfView, Is.InRange(zoomCap - 0.1f, zoomCap + 0.001f));
        foreach (var player in players)
        {
            Rect visible = BodyScreenBounds(player.GetComponentInChildren<Animator>().GetComponent<SpriteRenderer>(), camera, Vector2.zero);
            Assert.That(visible.xMin, Is.GreaterThanOrEqualTo(camera.pixelRect.xMin - 1));
            Assert.That(visible.xMax, Is.LessThanOrEqualTo(camera.pixelRect.xMax + 1));
            Assert.That(visible.yMin, Is.GreaterThanOrEqualTo(camera.pixelRect.yMin - 1));
            Assert.That(visible.yMax, Is.LessThanOrEqualTo(camera.pixelRect.yMax + 1));
        }
        for (int i = 0; i < devices.Count; i++)
            InputSystem.QueueStateEvent(devices[i], new GamepadState { leftStick = new Vector2(i % 2 == 0 ? 1 : -1, 0) });
        yield return new WaitForSeconds(2);
        foreach (var device in devices) InputSystem.QueueStateEvent(device, new GamepadState());
        yield return new WaitForSeconds(2);
        Assert.That(camera.fieldOfView, Is.LessThan(zoomCap - 0.5f));
#endif
    }

    [UnityTest]
    public IEnumerator FourPlayersAreTrackedDownedExcludedAndRevivedRestoredWithoutDuplicates()
    {
        yield return Load();
        var helper = Join(0); var target = Join(1); Join(2); Join(3);
        yield return null;
        var group = Object.FindAnyObjectByType<CinemachineTargetGroup>();
        var setter = Object.FindAnyObjectByType<CameraTargetSetter>();
        setter.SetTarget(target.transform); setter.SetTarget(target.transform);
        Assert.That(group.Targets.Count, Is.EqualTo(4));
        target.TakeDamage(10000);
        yield return null;
        Assert.That(group.Targets.Count, Is.EqualTo(3));
        Assert.That(group.FindMember(target.transform), Is.EqualTo(-1));
        target.ApplyRevivalWater(helper, 1000);
        yield return null;
        Assert.That(group.Targets.Count, Is.EqualTo(4));
        target.gameObject.SetActive(false);
        yield return null;
        Assert.That(group.Targets.Count, Is.EqualTo(3));
        foreach (var player in players)
            if (player.activeSelf) player.GetComponent<PlayerHealth>().TakeDamage(10000);
        yield return null;
        Assert.That(group.Targets.Count, Is.Zero);
    }
    [UnityTest]
    public IEnumerator TiltedCameraClampsAllEdgesPreservesTangentialMovementAndShowsRescueArrow()
    {
        yield return Load();
        var helper = Join(0); var target = Join(1);
        var controller = target.GetComponent<PlayerController>();
        var camera = Camera.main;
        camera.GetComponent<CinemachineBrain>().enabled = false;
        camera.transform.SetPositionAndRotation(new Vector3(0, -5, -8.660254f), Quaternion.Euler(-30, 0, 0));
        camera.orthographic = true;
        var body = target.GetComponentInChildren<Animator>().GetComponent<SpriteRenderer>();
        foreach (bool orthographic in new[] { true, false })
        {
            camera.orthographic = orthographic;
            camera.fieldOfView = 60;
            foreach (float padding in new[] { 0f, 20f })
            {
                typeof(PlayerController).GetField("screenEdgePaddingPixels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(controller, padding);
                foreach (float aspect in new[] { 16f/9f, 4f/3f, 0.75f })
                {
                    camera.aspect = aspect;
                    foreach (Vector2 outside in new[] { new Vector2(-100, 0), new Vector2(100, 0), new Vector2(0, -100), new Vector2(0, 100), new Vector2(100, 100) })
                    {
                        var clamped = controller.ConstrainToScreen(outside);
                        Rect visible = BodyScreenBounds(body, camera, clamped - (Vector2)target.transform.position);
                        Rect screen = camera.pixelRect;
                        Assert.That(visible.xMin, Is.GreaterThanOrEqualTo(screen.xMin + padding - 0.01f));
                        Assert.That(visible.xMax, Is.LessThanOrEqualTo(screen.xMax - padding + 0.01f));
                        Assert.That(visible.yMin, Is.GreaterThanOrEqualTo(screen.yMin + padding - 0.01f));
                        Assert.That(visible.yMax, Is.LessThanOrEqualTo(screen.yMax - padding + 0.01f));
                        float closestGap = Mathf.Min(visible.xMin - screen.xMin, screen.xMax - visible.xMax,
                            visible.yMin - screen.yMin, screen.yMax - visible.yMax);
                        Assert.That(closestGap, Is.EqualTo(padding).Within(0.02f), "Body should touch an edge without extra buffer.");
                        if (orthographic && outside.x < 0) Assert.That(visible.xMin, Is.EqualTo(screen.xMin + padding).Within(0.01f));
                        if (orthographic && outside.x > 0) Assert.That(visible.xMax, Is.EqualTo(screen.xMax - padding).Within(0.01f));
                        if (orthographic && outside.y < 0) Assert.That(visible.yMin, Is.EqualTo(screen.yMin + padding).Within(0.01f));
                        if (orthographic && outside.y > 0) Assert.That(visible.yMax, Is.EqualTo(screen.yMax - padding).Within(0.01f));
                    }
                    Vector2 first = controller.ConstrainToScreen(new Vector2(100, 0));
                    Vector2 second = controller.ConstrainToScreen(new Vector2(100, 1));
                    if (orthographic) Assert.That(second.y - first.y, Is.EqualTo(1).Within(0.001f));
                    Vector2 slide = controller.ConstrainToScreen(first + Vector2.down * 0.2f);
                    Assert.That((slide - first).magnitude, Is.GreaterThan(0.01f));
                }
            }
        }
        target.transform.position = new Vector3(100, 0, 0);
        target.TakeDamage(10000);
        yield return null;
        var marker = target.GetComponent<PlayerStatusMarker>();
        Assert.That(marker.IsRescueArrowVisible, Is.True);
        var arrow = Object.FindAnyObjectByType<PlayerRescueArrow>();
        Assert.That(arrow.color, Is.EqualTo(HoseWaterRules.ForPlayer(target.gameObject).GetHoseColor(1)));
        // Active state alone does not prove a custom UI Graphic can draw.
        Assert.That(arrow.canvasRenderer, Is.Not.Null, "The rescue arrow needs a CanvasRenderer.");
        Canvas.ForceUpdateCanvases();
        var mesh = arrow.canvasRenderer.GetMesh();
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh.vertexCount, Is.EqualTo(4));
        Assert.That(mesh.triangles.Length, Is.EqualTo(6));
        Assert.That(mesh.bounds.size.x, Is.GreaterThan(0));
        Assert.That(mesh.bounds.size.y, Is.GreaterThan(0));
        Assert.That(arrow.canvasRenderer.GetAlpha(), Is.GreaterThan(0));
        Assert.That(arrow.canvasRenderer.cull, Is.False);
        foreach (var color in mesh.colors32) Assert.That(color.a, Is.GreaterThan(0));
        var corners = new Vector3[4];
        arrow.rectTransform.GetWorldCorners(corners);
        foreach (var corner in corners)
            Assert.That(camera.pixelRect.Contains(new Vector2(corner.x, corner.y)), Is.True,
                "The rendered arrow must remain inside the screen.");
        Vector2 expectedDirection = (Vector2)camera.WorldToScreenPoint(target.transform.position) - camera.pixelRect.center;
        Assert.That(Vector2.Dot(arrow.rectTransform.right, expectedDirection.normalized), Is.GreaterThan(0.99f));
        target.transform.position = Vector3.zero;
        yield return null;
        Assert.That(marker.IsRescueArrowVisible, Is.False, "Returning on-screen hides the arrow.");
        target.transform.position = new Vector3(100, 0, 0);
        yield return null;
        Assert.That(marker.IsRescueArrowVisible, Is.True);
        Assert.That(target.transform.position.x, Is.EqualTo(100));
        target.ApplyRevivalWater(helper, 1000);
        yield return null;
        Assert.That(marker.IsRescueArrowVisible, Is.False);
    }
}

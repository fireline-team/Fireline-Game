using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.VFX;

/// <summary>Checks the rendered core/shot shape, not only the controller's property writes.</summary>
public sealed class HoseVfxRenderingTests
{
    private Scene _scene;
    private HoseVfxLibrary _library;
    private RenderTexture _target;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        if (!SystemInfo.supportsComputeShaders) Assert.Ignore("Requires a graphics-capable VFX Graph player.");
        var source = Resources.Load<HoseVfxLibrary>(HoseVfxLibrary.ResourcePath);
        Assert.That(source != null && source.GraphVersion >= 3, Is.True, "Upgrade the hose stream motion first.");
        _library = Object.Instantiate(source);
        _scene = SceneManager.CreateScene("Hose VFX rendering tests");
        var cameraObject = NewObject("Verification camera");
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(0f, 0f, -20f);
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.045f, 0.065f);
        _target = new RenderTexture(1280, 720, 24);
        _target.Create();
        camera.targetTexture = _target;
        yield return null;
    }

    private GameObject NewObject(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, _scene);
        return go;
    }

    private HoseGraphVisual Visual(NozzleType mode)
    {
        var effect = NewObject(mode.ToString()).AddComponent<HoseGraphVisual>();
        effect.Initialize(_library, mode, "Default", 20);
        return effect;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
        if (_target != null) { _target.Release(); Object.Destroy(_target); }
        if (_library != null) Object.Destroy(_library);
    }

    [UnityTest]
    public IEnumerator BeamRemainsContinuousWithZeroDropletsAndStopsCleanly()
    {
        _library.StreamAppearance.dropletRate = 0f;
        _library.StreamAppearance.beamGravity = 0f; // Isolate continuity from the separate curvature test.
        var stream = Visual(NozzleType.Standard);
        float readyAt = Time.time + 0.55f;
        while (Time.time < readyAt)
        {
            stream.SetSpraying(true, new Vector3(-4f, 3f, 0f), Vector2.right, 8f, 0.6f, 0.3f, 60f, 0.6f);
            yield return null;
        }
        var water = stream.transform.Find("Water").GetComponent<VisualEffect>();
        Assert.That(water.aliveParticleCount, Is.EqualTo(1), "The core is one particle, independent of droplet density.");
        var image = ReadImage();
        int covered = 0;
        for (int x = 430; x <= 850; x++)
        {
            Color pixel = image.GetPixel(x, 540);
            if (pixel.r + pixel.g + pixel.b > 1.1f) covered++;
        }
        Object.Destroy(image);
        Assert.That(covered, Is.GreaterThan(390), "The beam must form an unbroken line between muzzle and endpoint.");
        stream.Stop();
        yield return new WaitForSeconds(_library.StreamAppearance.releaseFade + 0.05f);
        var stoppedImage = ReadImage();
        Color stoppedPixel = stoppedImage.GetPixel(640, 540);
        Object.Destroy(stoppedImage);
        Assert.That(stoppedPixel.r + stoppedPixel.g + stoppedPixel.b, Is.LessThan(0.5f),
            "The core must disappear on release, independently of the asynchronous particle counter.");
        // Unity updates aliveParticleCount asynchronously about once per second.
        yield return new WaitForSeconds(1.1f);
        Assert.That(water.aliveParticleCount, Is.Zero, "Stopping the hose kills the persistent core.");
    }

    [UnityTest]
    public IEnumerator StreamExtendsBendsToGroundAndDissolvesAfterRelease()
    {
        var settings = _library.StreamAppearance;
        settings.dropletRate = 0f;
        settings.extensionSpeed = 10f;
        settings.beamGravity = 12f;
        settings.maximumDrop = 1f;
        settings.releaseFade = 0.15f;
        var stream = Visual(NozzleType.Standard);
        stream.SetSpraying(true, new Vector3(-4f, 3f, 0f), Vector2.right, 8f, 0.6f, 0.3f, 60f, 0.6f, 0.5f);
        var water = stream.transform.Find("Water").GetComponent<VisualEffect>();
        Assert.That(water.GetFloat("Range"), Is.LessThan(0.05f), "A new stream must start at the muzzle.");
        yield return new WaitForSeconds(0.12f);
        Assert.That(water.GetFloat("Range"), Is.InRange(0.2f, 4f), "The water front must advance before reaching full range.");
        var growing = ReadImage();
        File.WriteAllBytes("Temp/HoseStreamMotion-growing.png", growing.EncodeToPNG());
        Assert.That(growing.GetPixel(820, 540).grayscale, Is.LessThan(0.2f), "The distant end must not appear before water arrives.");
        Object.Destroy(growing);
        yield return new WaitForSeconds(0.85f);
        Assert.That(water.GetFloat("Range"), Is.EqualTo(8f).Within(0.01f));
        var full = ReadImage();
        int nearRow = BrightestRow(full, 430);
        int endRow = BrightestRow(full, 850);
        Assert.That(nearRow - endRow, Is.GreaterThan(15), "The rendered far end must bend downward.");
        File.WriteAllBytes("Temp/HoseStreamMotion-full.png", full.EncodeToPNG());
        Object.Destroy(full);
        Vector3[] vertices = water.GetMesh("BeamMesh").vertices;
        Vector3 end = (vertices[vertices.Length - 2] + vertices[vertices.Length - 1]) * 0.5f;
        Assert.That(-Vector3.Dot(end, TiltedView.Up), Is.EqualTo(0.5f).Within(0.01f), "Drop must be clamped to the muzzle's ground clearance.");

        stream.Stop();
        Assert.That(stream.IsEmitting, Is.False);
        yield return new WaitForSeconds(0.03f);
        var fading = ReadImage();
        Assert.That(fading.GetPixel(640, BrightestRow(fading, 640)).grayscale, Is.GreaterThan(0.2f), "Release must fade rather than pop out immediately.");
        File.WriteAllBytes("Temp/HoseStreamMotion-fading.png", fading.EncodeToPNG());
        Object.Destroy(fading);
        yield return new WaitForSeconds(0.18f);
        var gone = ReadImage();
        Assert.That(gone.GetPixel(640, endRow).grayscale, Is.LessThan(0.2f), "The beam must dissipate quickly.");
        Object.Destroy(gone);
        stream.SetSpraying(true, new Vector3(-4f, 3f, 0f), Vector2.right, 8f, 0.6f, 0.3f, 60f, 0.6f, 0.5f);
        Assert.That(water.GetFloat("Range"), Is.LessThan(0.05f), "Starting again after dissipation must extend from the muzzle again.");
        stream.Stop();
    }

    private static int BrightestRow(Texture2D image, int x)
    {
        float brightest = -1f;
        int row = 0;
        for (int y = 470; y < 560; y++)
        {
            float value = image.GetPixel(x, y).grayscale;
            if (value > brightest) { brightest = value; row = y; }
        }
        return row;
    }

    [UnityTest]
    public IEnumerator JetEmitsOneSpearAndComparisonRenders()
    {
        var stream = Visual(NozzleType.Standard);
        var mist = Visual(NozzleType.Mist);
        var jet = Visual(NozzleType.Jet);
        float readyAt = Time.time + 0.55f;
        while (Time.time < readyAt)
        {
            stream.SetSpraying(true, new Vector3(-4f, 3f, 0f), Vector2.right, 8f, 0.6f, 0.3f, 60f, 0.6f);
            mist.SetSpraying(true, new Vector3(-4f, 0f, 0f), Vector2.right, 3f, 3f, 0.35f, 90f, 0.9f);
            yield return null;
        }
        // Slow only this cloned test library so the moving spear is easy to inspect in a still frame.
        _library.JetAppearance.travelTime = 0.6f;
        jet.Pulse(new Vector3(-4f, -3f, 0f), Vector2.right, 8f, 0.25f, 0.2f, 0.45f, 200, false, Vector3.zero, Vector3.zero);
        jet.Stop();
        for (int i = 0; i < 6; i++) yield return null;
        var water = jet.transform.Find("Water").GetComponent<VisualEffect>();
        Assert.That(water.aliveParticleCount, Is.EqualTo(1), "A shot must stay one spear even with a legacy blob count of 200.");
        var image = ReadImage();
        Directory.CreateDirectory("Temp");
        File.WriteAllBytes("Temp/HoseVfxVerification.png", image.EncodeToPNG());
        Object.Destroy(image);
        stream.Stop();
        mist.Stop();
        yield return new WaitForSeconds(1.8f); // Includes expiry plus Unity's asynchronous counter refresh.
        Assert.That(water.aliveParticleCount, Is.Zero, "A released shot must finish and expire.");
    }

    private Texture2D ReadImage()
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = _target;
        var image = new Texture2D(_target.width, _target.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;
        return image;
    }
}

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
        Assert.That(source != null && source.GraphVersion >= 4, Is.True, "Upgrade water pixelation and contact splash first.");
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

    [UnityTest]
    public IEnumerator WaterPixelationChangesRenderedSamplingAndCanBeDisabled()
    {
        _library.StreamAppearance.dropletRate = 0f;
        _library.StreamAppearance.beamWidth = 1f;
        _library.StreamAppearance.beamGravity = 0f;
        _library.WaterPixelation.enabled = false;
        var stream = Visual(NozzleType.Standard);
        stream.SetSpraying(true, new Vector3(-4f, 1f, 0f), new Vector2(1f, 0.12f).normalized,
            8f, 0.6f, 0.3f, 60f, 0.6f);
        yield return new WaitForSeconds(0.55f);
        var smooth = ReadImage();
        _library.WaterPixelation.enabled = true;
        _library.WaterPixelation.pixelSize = 8;
        for (int i = 0; i < 4; i++) yield return null;
        var pixelated = ReadImage();
        File.WriteAllBytes("Temp/HoseWater-smooth.png", smooth.EncodeToPNG());
        File.WriteAllBytes("Temp/HoseWater-pixelated.png", pixelated.EncodeToPNG());
        int changed = 0;
        int flatPairs = 0;
        int smoothFlatPairs = 0;
        int visible = 0;
        for (int y = 400; y < 500; y++)
        for (int x = 480; x < 780; x++)
        {
            Color p = pixelated.GetPixel(x, y);
            Color s = smooth.GetPixel(x, y);
            if (p.grayscale < 0.3f) continue;
            visible++;
            if (Mathf.Abs(p.grayscale - s.grayscale) > 0.025f) changed++;
            // Interior samples in a block should share a sampled texture color.
            if (Mathf.Abs(p.grayscale - pixelated.GetPixel(x + 1, y).grayscale) < 0.002f) flatPairs++;
            if (Mathf.Abs(s.grayscale - smooth.GetPixel(x + 1, y).grayscale) < 0.002f) smoothFlatPairs++;
        }
        Object.Destroy(smooth);
        Object.Destroy(pixelated);
        Assert.That(visible, Is.GreaterThan(1000), "The water must render with the new VFX shader.");
        Assert.That(changed, Is.GreaterThan(500), "The toggle must change rendered pixels, not just a graph parameter.");
        Assert.That(flatPairs, Is.GreaterThan(smoothFlatPairs + 500), "Pixelated water must have visibly flat blocks.");
        _library.WaterPixelation.enabled = false;
        yield return null;
        yield return null;
        var water = stream.transform.Find("Water").GetComponent<VisualEffect>();
        Assert.That(water.GetFloat("PixelSize"), Is.EqualTo(1f));
        var tinted = ReadImage();
        Color center = tinted.GetPixel(640, 449);
        Assert.That(center.b - center.r, Is.GreaterThan(0.05f), "Shader Graph must preserve the beam's particle tint.");
        Object.Destroy(tinted);
        stream.Stop();
    }

    [UnityTest]
    public IEnumerator BeamSplashFlowsAtContactAndStopsWhenContactIsLost()
    {
        _library.StreamAppearance.dropletRate = 0f;
        _library.StreamAppearance.beamGravity = 0f;
        _library.SplashAppearance.streamRate = 180f;
        var stream = Visual(NozzleType.Standard);
        Vector3 origin = new Vector3(-4f, 1f, 0f);
        Vector3 impact = new Vector3(2f, 1f, 0f);
        stream.SetSpraying(true, origin, Vector2.right, 6f, 0.6f, 0.3f, 60f, 0.6f);
        var splash = stream.transform.Find("Impact splash").GetComponent<VisualEffect>();
        stream.ShowContinuousImpact(true, impact, Vector3.left);
        Assert.That(splash.GetFloat("ContactRate"), Is.Zero, "No splash before water arrives.");
        float until = Time.time + 1.4f;
        while (Time.time < until)
        {
            stream.SetSpraying(true, origin, Vector2.right, 6f, 0.6f, 0.3f, 60f, 0.6f);
            stream.ShowContinuousImpact(true, impact, Vector3.left);
            yield return null;
        }
        Assert.That(splash.GetFloat("ContactRate"), Is.EqualTo(180f));
        Assert.That(splash.aliveParticleCount, Is.GreaterThan(5), "The continuous spawner must actually produce particles.");
        var image = ReadImage();
        File.WriteAllBytes("Temp/HoseBeam-contact-splash.png", image.EncodeToPNG());
        File.WriteAllText("Temp/HoseBeam-contact-state.txt", string.Format(
            "particles={0}, origin={1}, direction={2}, size={3}, lifetime={4}, color={5}, bounds={6}, culled={7}",
            splash.aliveParticleCount, splash.GetVector3("Origin"), splash.GetVector3("Direction"),
            splash.GetFloat("ParticleSize"), splash.GetFloat("Lifetime"), splash.GetVector4("EffectColor"),
            splash.GetComponent<VFXRenderer>().bounds, splash.culled));
        int splashedPixels = 0;
        for (int y = 380; y < 490; y++)
        for (int x = 675; x < 790; x++)
            if (Mathf.Abs(y - 420) > 12 && image.GetPixel(x, y).grayscale > 0.15f) splashedPixels++;
        Object.Destroy(image);
        Assert.That(splashedPixels, Is.GreaterThan(15), "Splash droplets must be visible around the contact.");
        stream.ShowContinuousImpact(false, impact, Vector3.left);
        Assert.That(splash.GetFloat("ContactRate"), Is.Zero);
        stream.ShowContinuousImpact(true, impact - Vector3.right, Vector3.left);
        Assert.That(splash.GetVector3("Origin").x, Is.EqualTo(0.975f).Within(0.01f), "The reused emitter must follow a moving impact.");
        stream.Stop();
        Assert.That(splash.GetFloat("ContactRate"), Is.Zero, "Release must stop new splash droplets immediately.");
        yield return new WaitForSeconds(_library.SplashAppearance.lifetime + 1.1f);
        Assert.That(splash.aliveParticleCount, Is.Zero, "Existing splash droplets must finish and expire.");
    }
}

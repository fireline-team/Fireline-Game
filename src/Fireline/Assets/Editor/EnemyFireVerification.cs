using System;
using System.IO;
using System.Text;
using Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Prefab setup and isolated rendering checks for the emissive enemy sprite.</summary>
public static class EnemyFireVerification
{
    private const string MaterialPath = "Assets/_VFX/EnemyFire.mat";
    private const string PrefabPath = "Assets/_Prefabs/HordeEnemy.prefab";
    private const string RunningKey = "Fireline.EnemyFireTests.Running";
    private static EnemyFireResultWriter _callback;
    // Batchmode clears the validation project's Temp directory when Unity exits.
    public static string OutputDirectory => Environment.GetEnvironmentVariable("FIRELINE_ENEMY_FIRE_OUTPUT") ?? "Temp/EnemyFire";

    [InitializeOnLoadMethod]
    private static void OnReload()
    {
        if (SessionState.GetBool(RunningKey, false)) RegisterCallback();
        EditorApplication.delayCall += ProcessRequest;
    }

    private static void ProcessRequest()
    {
        const string request = "Temp/EnemyFire-setup-request";
        if (!File.Exists(request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ProcessRequest;
            return;
        }
        File.Delete(request);
        try
        {
            ApplyToPrefab();
            ValidateRendering();
            if (File.Exists("Temp/EnemyFire-tests-request"))
            {
                File.Delete("Temp/EnemyFire-tests-request");
                EditorApplication.delayCall += RunTests;
            }
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/EnemyFire-setup-error.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Fireline/Lighting/Apply Enemy Fire Material")]
    public static void ApplyToPrefab()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_VFX/EnemyFire.shader");
        if (shader == null) throw new Exception("Missing EnemyFire shader.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "EnemyFire" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var sprite = root.GetComponentInChildren<SpriteRenderer>(true);
            sprite.sharedMaterial = material;
            var visual = root.GetComponent<EnemyFireLight>();
            if (visual == null) visual = root.AddComponent<EnemyFireLight>();
            var child = root.transform.Find("Fire Light");
            if (child == null)
            {
                child = new GameObject("Fire Light").transform;
                child.SetParent(root.transform, false);
                child.localPosition = new Vector3(0f, 0.45f, -0.65f);
                var light = child.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                light.color = new Color(1f, 0.33f, 0.08f);
                light.intensity = 1.8f;
                light.range = 3.5f;
                light.shadows = LightShadows.None;
                light.bounceIntensity = 0f;
            }
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("spriteRenderer").objectReferenceValue = sprite;
            serialized.FindProperty("pointLight").objectReferenceValue = child.GetComponent<Light>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Fireline/Lighting/Validate Enemy Fire Rendering")]
    public static void ValidateRendering()
    {
        Directory.CreateDirectory(OutputDirectory);
        var report = new StringBuilder();
        Scene originalScene = SceneManager.GetActiveScene();
        bool originalDirty = originalScene.isDirty;
        var scene = EditorSceneManager.NewPreviewScene();
        var previous = RenderTexture.active;
        var target = new RenderTexture(768, 512, 24, RenderTextureFormat.ARGBHalf);
        var pixels = new Texture2D(768, 512, TextureFormat.RGBAFloat, false, true);
        var white = new Texture2D(2, 2);
        white.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        white.Apply();
        var whiteSprite = Sprite.Create(white, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2f);
        Material material = null;
        Material receiverMaterial = null;
        VolumeProfile profile = null;
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            root.transform.position = new Vector3(-1.3f, 0f, 0f);
            root.transform.localScale = Vector3.one * 1.7f;
            root.GetComponentInChildren<Animator>().enabled = false;
            var sprite = root.GetComponentInChildren<SpriteRenderer>();
            material = new Material(sprite.sharedMaterial);
            sprite.sharedMaterial = material;
            var light = root.GetComponentInChildren<Light>();
            light.transform.position = sprite.bounds.center + Vector3.back * 0.65f;
            light.range *= Mathf.Sqrt(1.7f);
            var receiverObject = new GameObject("Lit world sprite");
            SceneManager.MoveGameObjectToScene(receiverObject, scene);
            receiverObject.transform.position = new Vector3(0.8f, 0f, 0.2f);
            receiverObject.transform.localScale = new Vector3(1.8f, 2.6f, 1f);
            var receiver = receiverObject.AddComponent<SpriteRenderer>();
            receiver.sprite = whiteSprite;
            receiverMaterial = new Material(Shader.Find("Fireline/SpriteWorldLit"));
            receiver.sharedMaterial = receiverMaterial;
            receiver.color = new Color(0.35f, 0.35f, 0.35f);
            var cameraObject = new GameObject("Enemy fire verification camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.transform.position = new Vector3(0f, 0.8f, -8f);
            camera.fieldOfView = 30f;
            camera.aspect = 1.5f;
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.targetTexture = target;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();

            Color[] Read()
            {
                // Standard requests update volumes before rendering; SingleCameraRequest skips that step.
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 768, 512), 0, 0);
                pixels.Apply();
                return pixels.GetPixels();
            }
            void Save(string name, Color[] colors)
            {
                var png = new Texture2D(768, 512, TextureFormat.RGB24, false);
                for (int i = 0; i < colors.Length; i++) colors[i] = colors[i].gamma;
                png.SetPixels(colors);
                png.Apply();
                File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), png.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(png);
            }
            receiver.enabled = false;
            light.enabled = false;
            material.SetFloat("_EmissionIntensity", 0f);
            Color[] baseline = Read();
            Save("sprite-before", (Color[])baseline.Clone());
            material.SetFloat("_EmissionIntensity", 3.5f);
            Color[] emissive = Read();
            float peak = 0f;
            int silhouettePixels = 0;
            for (int i = 0; i < emissive.Length; i++)
            {
                peak = Mathf.Max(peak, emissive[i].maxColorComponent);
                bool originalPixel = Mathf.Max(baseline[i].r, baseline[i].g, baseline[i].b) > 0.000001f;
                bool glowingPixel = Mathf.Max(emissive[i].r, emissive[i].g, emissive[i].b) > 0.000001f;
                if (originalPixel != glowingPixel) throw new Exception("Emission changed the original sprite pixel mask.");
                if (originalPixel) silhouettePixels++;
                if (Mathf.Abs(emissive[i].a - baseline[i].a) > 0.001f)
                    throw new Exception("Emission changed the original sprite silhouette.");
            }
            if (silhouettePixels == 0 || silhouettePixels == emissive.Length) throw new Exception("Sprite silhouette was not visible in the render.");
            if (peak <= 1.1f) throw new Exception("Enemy sprite did not produce HDR emission: " + peak);
            report.AppendLine("PASS original sprite alpha preserved; HDR peak = " + peak);
            int tintCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { "Assets/_Configs" }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                sprite.color = definition.Tint;
                float tintPeak = 0f;
                foreach (Color pixel in Read()) tintPeak = Mathf.Max(tintPeak, pixel.maxColorComponent);
                if (tintPeak <= 1.1f) throw new Exception(definition.name + " tint did not retain HDR emission: " + tintPeak);
                tintCount++;
            }
            sprite.color = Color.white;
            report.AppendLine("PASS HDR glow retained across " + tintCount + " enemy definition tints");
            receiver.enabled = true;
            Color[] dark = Read();
            light.enabled = true;
            Color[] lit = Read();
            float spill = 0f;
            for (int y = 160; y < 350; y++)
                for (int x = 390; x < 590; x++)
                    spill = Mathf.Max(spill, lit[y * 768 + x].r - dark[y * 768 + x].r);
            if (spill < 0.005f) throw new Exception("Fire point light did not illuminate the adjacent world sprite: " + spill);
            report.AppendLine("PASS realtime point-light spill on SpriteWorldLit; red-channel increase = " + spill);

            var volumeObject = new GameObject("Verification bloom");
            SceneManager.MoveGameObjectToScene(volumeObject, scene);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10000f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            var levelProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/URPconfig/LevelRevampLightingVolume.asset");
            if (levelProfile == null) throw new Exception("Missing LevelRevamp lighting profile for bloom verification.");
            foreach (var component in levelProfile.components)
                profile.components.Add(UnityEngine.Object.Instantiate(component));
            cameraData.renderPostProcessing = true;
            receiver.enabled = false;
            light.enabled = false;
            Color[] bloomPixels = Read();
            int glowPixels = 0;
            for (int i = 0; i < bloomPixels.Length; i++)
                if (Mathf.Max(baseline[i].r, baseline[i].g, baseline[i].b) <= 0.000001f
                    && Mathf.Max(bloomPixels[i].r, bloomPixels[i].g, bloomPixels[i].b) > 0.0001f) glowPixels++;
            if (glowPixels == 0) throw new Exception("Bloom did not create a glow outside the sprite silhouette.");
            report.AppendLine("PASS bloom halo outside sprite silhouette: " + glowPixels + " pixels");
            receiver.enabled = true;
            light.enabled = true;
            Save("enemy-fire-lit", Read());
            report.AppendLine("PASS rendered preview with the level's bloom values and neutral tonemapping");
            foreach (var message in ShaderUtil.GetShaderMessages(material.shader))
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    throw new Exception(message.message);
            report.AppendLine("PASS shader compilation");
        }
        catch (Exception error) { report.AppendLine("FAIL " + error); Debug.LogException(error); }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            if (receiverMaterial != null) UnityEngine.Object.DestroyImmediate(receiverMaterial);
            if (profile != null)
            {
                foreach (var component in profile.components) UnityEngine.Object.DestroyImmediate(component);
                UnityEngine.Object.DestroyImmediate(profile);
            }
            UnityEngine.Object.DestroyImmediate(whiteSprite);
            UnityEngine.Object.DestroyImmediate(white);
            UnityEngine.Object.DestroyImmediate(pixels);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            report.AppendLine(SceneManager.GetActiveScene() == originalScene && originalScene.isDirty == originalDirty
                ? "PASS open scene preserved: " + originalScene.path : "FAIL open scene state changed");
            File.WriteAllText(Path.Combine(OutputDirectory, "render-validation.txt"), report.ToString());
            Debug.Log("[Enemy fire rendering] " + report);
        }
    }

    [MenuItem("Fireline/Lighting/Run Enemy Fire Tests")]
    public static void RunTests()
    {
        SessionState.SetBool(RunningKey, true);
        RegisterCallback();
        var runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { "EnemyFireTests", "EnemyTypeTests" } }));
    }

    public static void RunBatchChecks()
    {
        ValidateRendering();
        if (File.ReadAllText(Path.Combine(OutputDirectory, "render-validation.txt")).Contains("FAIL"))
            throw new Exception("Enemy fire rendering checks failed; see render-validation.txt.");
        RunTests();
    }

    private static void RegisterCallback()
    {
        if (_callback != null) TestRunnerApi.UnregisterTestCallback(_callback);
        _callback = new EnemyFireResultWriter();
        TestRunnerApi.RegisterTestCallback(_callback);
    }
}

public sealed class EnemyFireResultWriter : ICallbacks
{
    public void RunStarted(ITestAdaptor tests) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        SessionState.SetBool("Fireline.EnemyFireTests.Running", false);
        TestRunnerApi.UnregisterTestCallback(this);
        Directory.CreateDirectory(EnemyFireVerification.OutputDirectory);
        TestRunnerApi.SaveResultToFile(result, Path.Combine(EnemyFireVerification.OutputDirectory, "PlayMode-results.xml"));
        string summary = $"{result.ResultState}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.";
        File.WriteAllText(Path.Combine(EnemyFireVerification.OutputDirectory, "PlayMode-result.txt"), summary);
        Debug.Log("[Enemy fire tests] " + summary);
        if (Application.isBatchMode)
        {
            int exitCode = result.FailCount == 0 ? 0 : 1;
            EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
        }
    }
}

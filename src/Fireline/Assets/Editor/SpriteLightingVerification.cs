using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Render checks in a temporary scene, preserving the level designer's open scene.
public static class SpriteLightingVerification
{
    [InitializeOnLoadMethod]
    private static void OnReload()
    {
        const string request = "Temp/SpriteLighting-validate-request";
        if (!File.Exists(request)) return;
        File.Delete(request);
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Fireline/Lighting/Validate World Sprite Lighting")]
    public static void Run()
    {
        var report = new StringBuilder();
        var scene = EditorSceneManager.NewPreviewScene();
        var previous = RenderTexture.active;
        var target = new RenderTexture(128, 128, 24);
        var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        var texture = new Texture2D(2, 2);
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
        Material material = null;
        try
        {
            var cameraGo = new GameObject("Lighting verification camera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            var camera = cameraGo.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -3);
            camera.orthographic = false;
            camera.fieldOfView = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            camera.scene = scene;
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            var face = new GameObject("Sprite under test");
            SceneManager.MoveGameObjectToScene(face, scene);
            var renderer = face.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            var lightGo = new GameObject("Point light under test");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            lightGo.transform.position = new Vector3(0, 0, -1);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5;
            light.intensity = 0.5f;
            light.color = Color.red;
            foreach (string name in new[] { "Fireline/StructureDepth", "Fireline/SpriteWorldLit", "Fireline/SpriteFlash" })
            {
                var shader = Shader.Find(name);
                if (shader == null) throw new Exception("Missing shader " + name);
                material = new Material(shader);
                if (material.HasProperty("_FlashAmount")) material.SetFloat("_FlashAmount", 0);
                renderer.sharedMaterial = material;
                Color Read()
                {
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                    pixels.Apply();
                    return pixels.GetPixel(64, 64);
                }
                light.enabled = false;
                Color dark = Read();
                light.enabled = true;
                Color lit = Read();
                if (lit.r <= dark.r + 0.05f || lit.r <= lit.g + 0.05f)
                    throw new Exception(name + " did not respond to red point light: dark=" + dark + " lit=" + lit);
                renderer.flipX = renderer.flipY = true;
                Color flipped = Read();
                if (Mathf.Abs(flipped.r - lit.r) > 0.05f) throw new Exception(name + " changed lighting after flip");
                renderer.flipX = renderer.flipY = false;
                if (name == "Fireline/SpriteWorldLit")
                {
                    renderer.color = new Color(1, 1, 1, 0.35f);
                    light.enabled = false;
                    Color fadedDark = Read();
                    light.enabled = true;
                    Color faded = Read();
                    if (faded.r >= lit.r - 0.05f || faded.r <= fadedDark.r + 0.02f)
                        throw new Exception("Transparent fade failed: opaque=" + lit + " faded=" + faded + " faded dark=" + fadedDark);
                    renderer.color = Color.white;
                    report.AppendLine("PASS transparent fade retains light");
                }
                File.WriteAllBytes("Temp/" + name.Replace("Fireline/", "") + "-lighting.png", pixels.EncodeToPNG());
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                        throw new Exception(name + ": " + message.message);
                report.AppendLine("PASS " + name + " point light / flip / shader compilation: dark=" + dark + " lit=" + lit);
                UnityEngine.Object.DestroyImmediate(material);
                material = null;
            }
            report.AppendLine("PASS completed in isolated preview scene using perspective camera");
        }
        catch (Exception error) { report.AppendLine("FAIL " + error); Debug.LogException(error); }
        finally
        {
            RenderTexture.active = previous;
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            EditorSceneManager.ClosePreviewScene(scene);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
            File.WriteAllText("Temp/SpriteLighting-validation.txt", report.ToString());
            Debug.Log("[Sprite lighting validation] " + report);
        }
    }
}



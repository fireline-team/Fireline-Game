using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

public static partial class HoseVfxAssets
{
    private static readonly string[] PixelProperties =
        { "PixelSize", "PixelStrength", "PixelAspect", "PixelOffset", "ColorSteps", "AlphaCutoff" };

    [MenuItem("Fireline/VFX/Upgrade Hose Feel and Ambient Fire Smoke")]
    public static void UpgradeHoseFeelAndSmoke()
    {
        UpgradeWaterPixelation();
        var library = AssetDatabase.LoadAssetAtPath<HoseVfxLibrary>(Root + "/HoseVfxLibrary.asset");
        AssetDatabase.ImportAsset(Root + "/PixelatedWater.shadergraph", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        if (library.GraphVersion < 5)
        {
            foreach (var asset in new[] { library.Stream, library.Mist, library.Burst, library.Splash })
            {
                string path = AssetDatabase.GetAssetPath(asset);
                var model = OpenModel(path, false);
                PixelDefaults(model);
                if (asset == library.Stream)
                {
                    model.Defaults["CurveControl"] = Vector3.right * 2.5f;
                    model.Defaults["CurveEnd"] = Vector3.right * 5f;
                    model.Defaults["GroundDrop"] = 0f;
                    object block = ((IEnumerable)Get(model.Graph, "children")).Cast<object>()
                        .Where(c => c.GetType().Name == "VFXBasicInitialize")
                        .SelectMany(c => ((IEnumerable)Get(c, "children")).Cast<object>())
                        .Single(b => b.GetType().Name == "CustomHLSL" && (string)Call(b, "GetSettingValue", "m_BlockName") == "Shed droplets along the beam");
                    string code = DropletCode.Replace("in float MaxDrop)", "in float MaxDrop, in float3 CurveControl, in float3 CurveEnd, in float GroundDrop)")
                        .Replace("Origin + forward * (r * Range)", "Origin + 2.0 * (1.0 - r) * r * CurveControl + r * r * CurveEnd")
                        .Replace("min(MaxDrop, 0.5 * BeamGravity * travelTime * travelTime)",
                            "max(min(MaxDrop, 0.5 * BeamGravity * travelTime * travelTime), GroundDrop * r * r * r * r)");
                    Setting(block, "m_HLSLCode", code);
                    Call(block, "ResyncSlots", true);
                    BindInputs(model, block);
                }
                foreach (object output in ((IEnumerable)Get(model.Graph, "children")).Cast<object>()
                    .Where(c => c.GetType().Name == "VFXComposedParticleOutput").ToArray()) BindPixelInputs(model, output);
                SaveModel(model, path);
            }
            var serialized = new SerializedObject(library);
            var legacy = serialized.FindProperty("waterPixelation");
            foreach (var mode in new[] { NozzleType.Standard, NozzleType.Mist, NozzleType.Jet })
            {
                var settings = library.PixelationFor(mode);
                settings.enabled = legacy.FindPropertyRelative("enabled").boolValue;
                settings.pixelSize = legacy.FindPropertyRelative("pixelSize").intValue;
            }
            serialized.Update();
            serialized.FindProperty("graphVersion").intValue = 5;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
        }
        CreateFireSmokeAssets();
        foreach (var asset in new[] { library.Stream, library.Mist, library.Burst, library.Splash }) CheckProperties(asset, PixelProperties);
        CheckProperties(library.Stream, "CurveControl", "CurveEnd", "GroundDrop");
        Debug.Log("[Hose VFX] World-space endpoint lag, ground splashes, independent pixelation, and ambient fire smoke ready.");
    }

    private static void PixelDefaults(GraphModel model)
    {
        model.Defaults["PixelSize"] = 4f;
        model.Defaults["PixelStrength"] = 1f;
        model.Defaults["PixelAspect"] = Vector2.one;
        model.Defaults["PixelOffset"] = Vector2.zero;
        model.Defaults["ColorSteps"] = 0f;
        model.Defaults["AlphaCutoff"] = 0f;
    }

    private static void BindPixelInputs(GraphModel model, object output)
    {
        Call(output, "ResyncSlots", true);
        foreach (string name in PixelProperties)
        {
            object slot = Slots(output, "inputSlots").Single(s => (string)Get(s, "name") == name);
            if (!(bool)Call(slot, "HasLink", true)) Bind(model, slot, name);
        }
    }

    private static void CreateFireSmokeAssets()
    {
        const string directory = "Assets/Resources/FireVfx";
        const string path = directory + "/FireAmbientSmoke.vfx";
        Directory.CreateDirectory(directory);
        AssetDatabase.Refresh();
        if (!File.Exists(path))
        {
            var model = OpenModel(path, true);
            PixelDefaults(model);
            model.Defaults["PixelSize"] = 6f;
            model.Defaults["Rate"] = 14f;
            model.Defaults["Lifetime"] = 2.8f;
            model.Defaults["ParticleSize"] = 0.8f;
            model.Defaults["Radius"] = 0.65f;
            model.Defaults["Speed"] = 0.75f;
            model.Defaults["Spread"] = 0.25f;
            model.Defaults["Growth"] = 2.4f;
            model.Defaults["EffectColor"] = new Vector4(0.27f, 0.29f, 0.32f, 0.58f);
            var smoke = System(model, "Ambient fire smoke", 512u, SmokeTexture(), 250);
            Spawn(model, smoke.Spawn, false);
            Hlsl(model, smoke.Init, "Emit rising smoke across the fire", SmokeInitialize);
            Hlsl(model, smoke.Output, "Expand and fade smoke", SmokeFade);

            var captured = new List<(object block, string[] path, object value, object[] sources)>();
            foreach (object block in ((IEnumerable)Get(smoke.Output, "children")))
            foreach (object slot in Slots(block, "inputSlots")) CaptureSlotLinks(block, slot, new string[0], captured);
            object shader = Call(TypeOf("UnityEditor.VFX.VFXShaderGraphHelpers"), "LoadShaderGraphAssetAtPath", Root + "/PixelatedWater.shadergraph");
            object[] previous = ((IEnumerable)Get(model.Graph, "children")).Cast<object>().ToArray();
            Setting(smoke.Output, "shaderGraph", shader);
            Call(TypeOf("UnityEditor.VFX.SanitizeHelper"), "MigrateSGOutputToComposed", smoke.Output);
            object output = ((IEnumerable)Get(model.Graph, "children")).Cast<object>()
                .Single(c => !previous.Contains(c) && c.GetType().Name == "VFXComposedParticleOutput");
            Set(Slots(output, "inputSlots").First(s => (string)Get(s, "name") == "_BaseMap"), "value", SmokeTexture());
            foreach (object block in ((IEnumerable)Get(output, "children"))) Call(block, "ResyncSlots", true);
            foreach (var state in captured.Where(s => s.path.Length == 1)) Set(FindBlockSlot(state.block, state.path), "value", state.value);
            foreach (var state in captured)
            foreach (object source in state.sources)
                if (!(bool)Call(FindBlockSlot(state.block, state.path), "Link", source)) throw new InvalidOperationException("Could not restore smoke input.");
            BindParticleTint(model, output);
            BindPixelInputs(model, output);
            SaveModel(model, path);
        }
        var graph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
        CheckProperties(graph, "Rate", "Origin", "Up", "Radius", "Growth", "EffectColor", "PixelSize", "PixelStrength");
        const string profilePath = directory + "/FireSmokeProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<FireSmokeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<FireSmokeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("graph").objectReferenceValue = graph;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(profile);
        }
        const string prefabPath = "Assets/_Prefabs/FireAmbientSmoke.prefab";
        if (!File.Exists(prefabPath))
        {
            var go = new GameObject("Fire ambient smoke");
            try
            {
                go.AddComponent<FireSmokeVisual>().Configure(null, profile, null);
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }

    private static Texture2D SmokeTexture()
    {
        const string path = "Assets/Resources/FireVfx/SmokeCloud.asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture != null) return texture;
        texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
        { name = "Billowing smoke cloud", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color[128 * 128];
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 128; x++)
        {
            float u = (x + 0.5f) / 64f - 1f;
            float v = (y + 0.5f) / 64f - 1f;
            float noise = Mathf.PerlinNoise(u * 3f + 7.1f, v * 3f + 12.4f);
            float radius = Mathf.Sqrt(u * u + v * v) + (noise - 0.5f) * 0.3f;
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - radius) / 0.4f)) * Mathf.Lerp(0.45f, 1f, noise);
            float shade = Mathf.Lerp(0.6f, 1f, noise);
            pixels[y * 128 + x] = new Color(shade, shade, shade, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    private const string SmokeInitialize = @"void InitializeSmoke(inout VFXAttributes attributes, in float3 Origin, in float3 Up, in float Radius, in float Lifetime, in float ParticleSize, in float SizeVariation, in float Speed, in float Spread, in float4 EffectColor)
{
    float r = frac(sin((float(attributes.particleId) + 1.0) * 12.9898) * 43758.5453);
    float s = frac(sin((float(attributes.particleId) + 2.0) * 78.233) * 23421.631);
    float q = frac(sin((float(attributes.particleId) + 3.0) * 39.425) * 17834.345);
    float angle = r * 6.283185;
    float3 radial = float3(cos(angle), sin(angle), 0);
    attributes.position = Origin + radial * sqrt(s) * Radius;
    attributes.velocity = Up * Speed * lerp(0.7, 1.2, q) + radial * Spread;
    attributes.age = 0;
    attributes.lifetime = Lifetime * lerp(0.8, 1.0, q);
    attributes.size = ParticleSize * lerp(1.0 - SizeVariation, 1.0 + SizeVariation, s);
    attributes.color = EffectColor.xyz;
    attributes.alpha = EffectColor.w;
}";

    private const string SmokeFade = @"void FadeSmoke(inout VFXAttributes attributes, in float Growth)
{
    float t = saturate(attributes.age / max(0.05, attributes.lifetime));
    attributes.alpha *= smoothstep(0.0, 0.12, t) * (1.0 - smoothstep(0.5, 1.0, t));
    attributes.size *= lerp(1.0, Growth, t);
}";
}

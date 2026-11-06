using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

public static partial class HoseVfxAssets
{
    private sealed class GraphModel
    {
        public object Resource;
        public object Graph;
        public readonly Dictionary<string, object> Parameters = new Dictionary<string, object>();
        public readonly Dictionary<string, object> Defaults = new Dictionary<string, object>
        {
            { "Origin", Vector3.zero }, { "Direction", Vector3.right }, { "Up", TiltedView.Up },
            { "Range", 5f }, { "Width", 0.6f }, { "Lifetime", 0.6f }, { "ParticleSize", 0.065f },
            { "Rate", 55f }, { "Count", 1f }, { "Active", 1f }, { "BeamWidth", 0.22f },
            { "BeamColor", new Vector4(0.7f, 0.92f, 1f, 0.95f) },
            { "EffectColor", new Vector4(0.52f, 0.82f, 1f, 0.8f) },
            { "Speed", 2f }, { "Spread", 0.7f }, { "FallSpeed", 0.35f },
            { "Gravity", -TiltedView.Up * 4f }, { "SizeVariation", 0.35f },
            { "Length", 1.8f }, { "SpearWidth", 0.45f }, { "FadeStart", 0.85f },
            { "WaterSpeed", 20f }, { "BeamGravity", 18f }, { "MaxDrop", 0.75f }
        };
    }

    private sealed class ParticleSystemModel
    {
        public object Spawn;
        public object Init;
        public object Update;
        public object Output;
    }

    [MenuItem("Fireline/VFX/Upgrade Stream and Jet Graphs")]
    public static void UpgradeStreamAndJet()
    {
        var library = AssetDatabase.LoadAssetAtPath<HoseVfxLibrary>(Root + "/HoseVfxLibrary.asset");
        if (library == null) throw new InvalidOperationException("Create the hose VFX library first.");
        if (library.GraphVersion >= 2)
        {
            ValidateRevisedAssets(library);
            Debug.Log("[Hose VFX] Beam and spear graphs are already upgraded; artist edits preserved.");
            return;
        }
        // Modify graph models through Unity and retain the existing asset GUIDs/references.
        BuildStreamOrSpear(AssetDatabase.GetAssetPath(library.Stream), true);
        BuildStreamOrSpear(AssetDatabase.GetAssetPath(library.Burst), false);
        AddContactAppearance(AssetDatabase.GetAssetPath(library.Splash), false);
        AddContactAppearance(AssetDatabase.GetAssetPath(library.Steam), true);
        ValidateRevisedAssets(library);
        var serialized = new SerializedObject(library);
        serialized.FindProperty("graphVersion").intValue = 2;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log("[Hose VFX] Upgraded: one continuous beam core, independent falling droplets, one spear per jet shot, and Inspector appearance controls. Mist graph preserved.");
    }

    [MenuItem("Fireline/VFX/Upgrade Stream Motion")]
    public static void UpgradeStreamMotion()
    {
        var library = AssetDatabase.LoadAssetAtPath<HoseVfxLibrary>(Root + "/HoseVfxLibrary.asset");
        if (library == null) throw new InvalidOperationException("Create the hose VFX library first.");
        if (library.GraphVersion < 2) UpgradeStreamAndJet();
        if (library.GraphVersion < 3)
        {
            BuildStreamOrSpear(AssetDatabase.GetAssetPath(library.Stream), true);
            var serialized = new SerializedObject(library);
            serialized.FindProperty("graphVersion").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
        CheckProperties(library.Stream, "BeamMesh", "WaterSpeed", "BeamGravity", "MaxDrop", "Active");
        Debug.Log("[Hose VFX] Stream motion ready: advancing curved ribbon and release fade. Jet and mist assets preserved.");
    }

    private static GraphModel OpenModel(string path, bool clear)
    {
        if (!File.Exists(path)) Call(TypeOf("UnityEditor.VisualEffectAssetEditorUtility"), "CreateNewAsset", path);
        var model = new GraphModel();
        model.Resource = Call(TypeOf("UnityEditor.VFX.VisualEffectResource"), "GetResourceAtPath", path);
        model.Graph = Call(TypeOf("UnityEditor.VFX.VisualEffectResourceExtensions"), "GetGraph", model.Resource);
        object[] children = ((IEnumerable)Get(model.Graph, "children")).Cast<object>().ToArray();
        foreach (object child in children)
        {
            if (clear) Call(model.Graph, "RemoveChild", child);
            else if (child.GetType().Name == "VFXParameter") model.Parameters[(string)Get(child, "exposedName")] = child;
        }
        return model;
    }

    private static void SaveModel(GraphModel model, string path)
    {
        Call(TypeOf("UnityEditor.VFX.VisualEffectResourceExtensions"), "WriteAssetWithSubAssets", model.Resource);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static ParticleSystemModel System(GraphModel model, string label, uint capacity, Texture2D texture, float x, bool mesh = false)
    {
        var system = new ParticleSystemModel
        {
            Spawn = Node(model.Graph, "VFXBasicSpawner", new Vector2(x, -400)),
            Init = Node(model.Graph, "VFXBasicInitialize", new Vector2(x, -100)),
            Update = Node(model.Graph, "VFXBasicUpdate", new Vector2(x, 350)),
            Output = Node(model.Graph, mesh ? "VFXMeshOutput" : "VFXPlanarPrimitiveOutput", new Vector2(x, 700))
        };
        Set(system.Init, "label", label);
        Call(system.Init, "LinkFrom", system.Spawn);
        Call(system.Update, "LinkFrom", system.Init);
        Call(system.Output, "LinkFrom", system.Update);
        SetEnumProperty(system.Init, "space", "World");
        object data = Call(system.Init, "GetData");
        Setting(data, "capacity", capacity);
        SettingEnum(data, "boundsMode", "Automatic");
        SettingEnum(system.Output, "blendMode", "Alpha");
        SettingEnum(system.Output, "cullMode", "Off");
        Set(Slots(system.Output, "inputSlots").First(s => (string)Get(s, "name") == "mainTexture"), "value", texture);
        if (mesh)
            Bind(model, Slots(system.Output, "inputSlots").First(s => (string)Get(s, "name") == "mesh"), "BeamMesh");
        else
        {
            object orient = New("UnityEditor.VFX.Block.Orient");
            SettingEnum(orient, "mode", "FaceCameraPlane");
            Call(system.Output, "AddChild", orient);
        }
        return system;
    }

    private static void Spawn(GraphModel model, object context, bool burst, bool exposeCount = true)
    {
        object block = New(burst ? "UnityEditor.VFX.VFXSpawnerBurst" : "UnityEditor.VFX.VFXSpawnerConstantRate");
        Call(context, "AddChild", block);
        object slot = Slots(block, "inputSlots").First();
        if (exposeCount) Bind(model, slot, burst ? "Count" : "Rate");
        else Set(slot, "value", 1f);
    }

    private static object Hlsl(GraphModel model, object context, string label, string code)
    {
        object block = New("UnityEditor.VFX.Block.CustomHLSL");
        Setting(block, "m_BlockName", label);
        Setting(block, "m_HLSLCode", code);
        Call(context, "AddChild", block);
        BindInputs(model, block);
        return block;
    }

    private static void BindInputs(GraphModel model, object block)
    {
        foreach (object slot in Slots(block, "inputSlots")) Bind(model, slot, ((string)Get(slot, "name")).TrimStart('_'));
    }

    private static void Bind(GraphModel model, object slot, string name)
    {
        if (!model.Parameters.TryGetValue(name, out object parameter))
        {
            if (!model.Defaults.TryGetValue(name, out object value)) throw new InvalidOperationException("No default for " + name);
            parameter = New("UnityEditor.VFX.VFXParameter");
            Call(parameter, "Init", value.GetType());
            Setting(parameter, "m_ExposedName", name);
            Setting(parameter, "m_Exposed", true);
            Set(parameter, "value", value);
            Call(model.Graph, "AddChild", parameter);
            model.Parameters.Add(name, parameter);
        }
        object output = Slots(parameter, "outputSlots").First();
        if (!(bool)Call(slot, "Link", output)) throw new InvalidOperationException("Could not bind " + name);
        int id = (int)Call(parameter, "AddNode", new Vector2(-350, -400 + model.Parameters.Count * 100));
        object node = Call(parameter, "GetNode", id);
        Type linkType = TypeOf("UnityEditor.VFX.VFXParameter+NodeLinkedSlot");
        object link = Activator.CreateInstance(linkType);
        linkType.GetField("inputSlot").SetValue(link, slot);
        linkType.GetField("outputSlot").SetValue(link, output);
        var links = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(linkType));
        links.Add(link);
        node.GetType().GetField("linkedSlots").SetValue(node, links);
    }

    private static void BuildStreamOrSpear(string path, bool stream)
    {
        var model = OpenModel(path, true);
        if (stream)
        {
            model.Defaults["BeamMesh"] = ArcTemplateMesh();
            var core = System(model, "Continuous curved ribbon (one particle)", 1u, RibbonTexture(false), 250, true);
            Spawn(model, core.Spawn, true, false);
            Setting(core.Update, "ageParticles", false);
            Setting(core.Update, "reapParticles", false);
            SettingEnum(core.Update, "integration", "None");
            Hlsl(model, core.Init, "Create one continuous water core", CoreCode);
            Hlsl(model, core.Update, "Follow muzzle and stop with trigger", CoreCode);

            var drops = System(model, "Droplets falling off the stream", 2048u, CreateTexture(), 1000);
            Spawn(model, drops.Spawn, false);
            Hlsl(model, drops.Init, "Shed droplets along the beam", DropletCode);
            object gravity = New("UnityEditor.VFX.Block.Gravity");
            Call(drops.Update, "AddChild", gravity);
            Bind(model, Slots(gravity, "inputSlots").First(), "Gravity");
            Hlsl(model, drops.Output, "Fade detached droplets", DropletFadeCode);
        }
        else
        {
            model.Defaults["Lifetime"] = 0.12f;
            model.Defaults["EffectColor"] = new Vector4(0.68f, 0.91f, 1f, 1f);
            var spear = System(model, "Single pointed water spear", 64u, RibbonTexture(true), 250);
            Spawn(model, spear.Spawn, true);
            Hlsl(model, spear.Init, "Launch one water spear", SpearCode);
            Hlsl(model, spear.Output, "Align spear and fade its tail", SpearOrientationCode);
        }
        SaveModel(model, path);
    }

    private const string CoreCode = @"void WaterCore(inout VFXAttributes attributes, in float3 Origin, in float4 BeamColor, in float Active)
{
    attributes.position = Origin;
    attributes.velocity = float3(0,0,0);
    attributes.age = 0;
    attributes.lifetime = 1;
    attributes.size = 1;
    attributes.scaleX = 1;
    attributes.scaleY = 1;
    attributes.scaleZ = 1;
    attributes.axisX = float3(1,0,0);
    attributes.axisY = float3(0,1,0);
    attributes.axisZ = float3(0,0,1);
    attributes.color = BeamColor.xyz;
    attributes.alpha = BeamColor.w;
    attributes.alive = Active > 0.5;
}";

    private const string DropletCode = @"void ShedDroplets(inout VFXAttributes attributes, in float3 Origin, in float3 Direction, in float3 Up, in float Range, in float Width, in float Lifetime, in float ParticleSize, in float BeamWidth, in float SizeVariation, in float Speed, in float Spread, in float FallSpeed, in float4 EffectColor, in float WaterSpeed, in float BeamGravity, in float MaxDrop)
{
    float r = frac(sin((float(attributes.particleId) + 1.0) * 12.9898) * 43758.5453);
    float s = frac(sin((float(attributes.particleId) + 2.0) * 78.233) * 23421.631);
    float q = frac(sin((float(attributes.particleId) + 3.0) * 39.425) * 17834.345);
    float3 forward = normalize(Direction);
    float3 across = float3(-forward.y, forward.x, 0);
    attributes.position = Origin + forward * (r * Range) + across * (s - 0.5) * BeamWidth;
    float travelTime = r * Range / max(0.1, WaterSpeed);
    attributes.position -= Up * min(MaxDrop, 0.5 * BeamGravity * travelTime * travelTime);
    float forwardSpeed = min(Speed, Range * (1.0 - r) / max(0.05, Lifetime));
    attributes.velocity = forward * forwardSpeed + across * (s - 0.5) * Spread - Up * FallSpeed;
    attributes.age = 0;
    attributes.lifetime = Lifetime;
    attributes.size = ParticleSize * lerp(1.0 - SizeVariation, 1.0 + SizeVariation, q);
    attributes.color = EffectColor.xyz;
    attributes.alpha = EffectColor.w;
}";

    private const string DropletFadeCode = @"void FadeDroplets(inout VFXAttributes attributes)
{
    float t = saturate(attributes.age / max(0.02, attributes.lifetime));
    attributes.alpha *= 1.0 - smoothstep(0.35, 1.0, t);
}";

    private const string SpearCode = @"void LaunchSpear(inout VFXAttributes attributes, in float3 Origin, in float3 Direction, in float3 Up, in float Range, in float Width, in float Lifetime, in float ParticleSize, in float Length, in float SpearWidth, in float4 EffectColor)
{
    float3 forward = normalize(Direction);
    float shotLength = min(Length, max(0.001, Range));
    attributes.position = Origin + forward * shotLength * 0.5;
    attributes.velocity = forward * (max(0.001, Range - shotLength) / max(0.02, Lifetime));
    attributes.age = 0;
    attributes.lifetime = Lifetime;
    attributes.size = 1;
    attributes.scaleX = shotLength;
    attributes.scaleY = SpearWidth;
    attributes.scaleZ = 1;
    attributes.color = EffectColor.xyz;
    attributes.alpha = EffectColor.w;
}";

    private static Mesh ArcTemplateMesh()
    {
        const string path = Root + "/WaterArcMesh.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = "Water ribbon template" };
        mesh.vertices = new[] { new Vector3(0,-0.5f,0), new Vector3(0,0.5f,0), new Vector3(1,-0.5f,0), new Vector3(1,0.5f,0) };
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one };
        mesh.triangles = new[] { 0,2,1, 1,2,3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private const string SpearOrientationCode = @"void AimSpear(inout VFXAttributes attributes, in float FadeStart)
{
    float3 forward = normalize(attributes.velocity);
    attributes.axisY = normalize(cross(attributes.axisZ, forward));
    attributes.axisX = forward;
    attributes.axisZ = normalize(cross(attributes.axisX, attributes.axisY));
    float t = saturate(attributes.age / max(0.02, attributes.lifetime));
    attributes.alpha *= 1.0 - smoothstep(FadeStart, 1.0, t);
}";

    private static Texture2D RibbonTexture(bool spear)
    {
        string path = Root + (spear ? "/WaterSpear.asset" : "/WaterBeam.asset");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture != null) return texture;
        texture = new Texture2D(256, 64, TextureFormat.RGBA32, false, true)
        {
            name = spear ? "Pointed water spear" : "Continuous water ribbon",
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        var pixels = new Color[256 * 64];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 256; x++)
        {
            float u = (x + 0.5f) / 256f;
            float v = Mathf.Abs((y + 0.5f) / 32f - 1f);
            float halfWidth = spear ? (u < 0.65f ? Mathf.Lerp(0.01f, 0.95f, u / 0.65f) : 0.95f * (1f - u) / 0.35f)
                : 0.85f + 0.025f * Mathf.Sin(u * 55f);
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((halfWidth - v) * 12f));
            float ends = Mathf.SmoothStep(0f, 1f, u * 80f) * Mathf.SmoothStep(0f, 1f, (1f - u) * 80f);
            Color color = Color.Lerp(new Color(0.18f, 0.65f, 0.95f), Color.white, Mathf.Pow(Mathf.Clamp01(1f - v / Mathf.Max(0.01f, halfWidth)), 2f));
            color.a = edge * ends;
            pixels[y * 256 + x] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    private static void AddContactAppearance(string path, bool steam)
    {
        var model = OpenModel(path, false);
        model.Defaults["EffectColor"] = steam ? new Vector4(0.86f, 0.91f, 0.95f, 0.32f) : new Vector4(0.52f, 0.82f, 1f, 0.85f);
        model.Defaults["Speed"] = 1f;
        model.Defaults["Spread"] = steam ? 0.5f : 4f;
        object[] contexts = ((IEnumerable)Get(model.Graph, "children")).Cast<object>().ToArray();
        foreach (object context in contexts.Where(c => c.GetType().Name == "VFXBasicInitialize" || c.GetType().Name == "VFXPlanarPrimitiveOutput"))
        foreach (object block in ((IEnumerable)Get(context, "children")).Cast<object>().Where(b => b.GetType().Name == "CustomHLSL"))
        {
            bool init = context.GetType().Name == "VFXBasicInitialize";
            string code = init ? InitializeCode(steam ? 4 : 3) : UpdateCode(steam ? 4 : 3);
            if (init)
            {
                code = code.Replace("in float ParticleSize)", "in float ParticleSize, in float4 EffectColor, in float Speed, in float Spread)")
                    .Replace("attributes.color = float3(0.52, 0.82, 1.0);", "attributes.color = EffectColor.xyz;")
                    .Replace("attributes.alpha = 1;", "attributes.alpha = EffectColor.w;")
                    .Replace("attributes.color = float3(0.86, 0.91, 0.95);", "attributes.color = EffectColor.xyz;");
                code = steam ? code.Replace("Up * lerp(0.55, 1.0, s)", "Up * lerp(0.55, 1.0, s) * Speed")
                    .Replace("(r - 0.5) * 0.5", "(r - 0.5) * Spread")
                    : code.Replace("forward * lerp(0.6, 2.3, q)", "forward * lerp(0.6, 2.3, q) * Speed")
                    .Replace("(r - 0.5) * 4.0", "(r - 0.5) * Spread");
            }
            else code = code.Replace("inout VFXAttributes attributes)", "inout VFXAttributes attributes, in float4 EffectColor)")
                .Replace(steam ? "attributes.alpha = 0.32" : "attributes.alpha = 0.85", "attributes.alpha = EffectColor.w");
            Setting(block, "m_HLSLCode", code);
            BindInputs(model, block);
        }
        SaveModel(model, path);
    }

    private static void ValidateRevisedAssets(HoseVfxLibrary library)
    {
        CheckProperties(library.Stream, "Active", "BeamWidth", "BeamColor", "Rate", "Gravity", "EffectColor", "SizeVariation");
        CheckProperties(library.Burst, "Count", "Length", "SpearWidth", "Lifetime", "EffectColor", "FadeStart");
        CheckProperties(library.Splash, "EffectColor", "Speed", "Spread");
        CheckProperties(library.Steam, "EffectColor", "Speed", "Spread");
    }

    private static void CheckProperties(VisualEffectAsset asset, params string[] names)
    {
        var properties = new List<VFXExposedProperty>();
        asset.GetExposedProperties(properties);
        foreach (string name in names)
            if (!properties.Any(p => p.name == name)) throw new InvalidOperationException(asset.name + " is missing compiled property " + name);
    }
}

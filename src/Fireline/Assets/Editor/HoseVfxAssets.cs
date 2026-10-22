using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

/// <summary>Authors ordinary, editable VFX Graph assets through the installed 17.6 graph model.</summary>
public static class HoseVfxAssets
{
    private const string Root = "Assets/Resources/HoseVfx";
    private const string Request = "Temp/FirelineHoseVfx.request";
    private const string Report = "Temp/FirelineHoseVfx-report.txt";
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [InitializeOnLoadMethod]
    private static void OnLoad()
    {
        // A local, untracked request lets file-based authoring finish after Unity's next refresh.
        // Collaborators importing these scripts do not get unsolicited asset generation.
        if (File.Exists(Request)) EditorApplication.delayCall += ProcessRequest;
    }

    private static void ProcessRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessRequest;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try
        {
            CreateMissingAssets();
            File.WriteAllText(Report, "PASS: five hose graphs, shared library, and preview scene created and imported.\n");
        }
        catch (Exception exception)
        {
            File.WriteAllText(Report, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Fireline/VFX/Create Missing Hose Graphs")]
    public static void CreateMissingAssets()
    {
        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        Texture2D texture = CreateTexture();
        string[] names = { "Stream", "Mist", "Burst", "Splash", "Steam" };
        var assets = new VisualEffectAsset[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            string path = $"{Root}/Hose{names[i]}.vfx";
            if (!File.Exists(path)) BuildGraph(path, i, texture);
            assets[i] = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (assets[i] == null) throw new InvalidOperationException("Graph failed to import: " + path);
            var properties = new List<VFXExposedProperty>();
            assets[i].GetExposedProperties(properties);
            string[] required = { "Origin", "Direction", "Up", "Range", "Width", "Lifetime", "ParticleSize", i < 2 ? "Rate" : "Count" };
            foreach (string property in required)
                if (!properties.Any(p => p.name == property))
                    throw new InvalidOperationException(path + " is missing the compiled property " + property);
        }
        string libraryPath = Root + "/HoseVfxLibrary.asset";
        var library = AssetDatabase.LoadAssetAtPath<HoseVfxLibrary>(libraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<HoseVfxLibrary>();
            AssetDatabase.CreateAsset(library, libraryPath);
            var serialized = new SerializedObject(library);
            for (int i = 0; i < names.Length; i++)
                serialized.FindProperty(names[i].ToLowerInvariant()).objectReferenceValue = assets[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        CreatePreviewScene(library);
        Debug.Log("[Hose VFX] Created missing hose assets. Existing graphs and level scenes were preserved.");
    }

    private static Texture2D CreateTexture()
    {
        string path = Root + "/SoftDroplet.asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture != null) return texture;
        texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true)
        {
            name = "SoftDroplet", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        var pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float radius = new Vector2((x + 0.5f) / 32f - 1f, (y + 0.5f) / 32f - 1f).magnitude;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius * radius), 2f);
            pixels[y * 64 + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    private static void BuildGraph(string path, int kind, Texture2D texture)
    {
        // The graph-model API is internal to the package. Keep this version-sensitive
        // reflection in an Editor authoring tool; runtime only uses the public VFX API.
        Call(TypeOf("UnityEditor.VisualEffectAssetEditorUtility"), "CreateNewAsset", path);
        object resource = Call(TypeOf("UnityEditor.VFX.VisualEffectResource"), "GetResourceAtPath", path);
        object graph = Call(TypeOf("UnityEditor.VFX.VisualEffectResourceExtensions"), "GetGraph", resource);
        object spawn = Node(graph, "VFXBasicSpawner", new Vector2(250, -400));
        object init = Node(graph, "VFXBasicInitialize", new Vector2(250, -100));
        object update = Node(graph, "VFXBasicUpdate", new Vector2(250, 400));
        object output = Node(graph, "VFXPlanarPrimitiveOutput", new Vector2(250, 750));
        Call(init, "LinkFrom", spawn);
        Call(update, "LinkFrom", init);
        Call(output, "LinkFrom", update);
        SetEnumProperty(init, "space", "World");
        object data = Call(init, "GetData");
        Setting(data, "capacity", kind < 2 ? 2048u : 512u);
        SettingEnum(data, "boundsMode", "Automatic");
        SettingEnum(output, "blendMode", "Alpha");
        object mainTexture = Slots(output, "inputSlots").First(s => (string)Get(s, "name") == "mainTexture");
        Set(mainTexture, "value", texture);
        object orient = New("UnityEditor.VFX.Block.Orient");
        SettingEnum(orient, "mode", "FaceCameraPlane");
        Call(output, "AddChild", orient);

        object spawner = New(kind < 2 ? "UnityEditor.VFX.VFXSpawnerConstantRate" : "UnityEditor.VFX.VFXSpawnerBurst");
        Call(spawn, "AddChild", spawner);
        object spawnInput = Slots(spawner, "inputSlots").First();
        Property(graph, spawnInput, kind < 2 ? "Rate" : "Count", typeof(float), kind < 2 ? 360f : 24f, 0);

        object initialize = New("UnityEditor.VFX.Block.CustomHLSL");
        Setting(initialize, "m_BlockName", "Initialize hose " + new[] { "stream", "mist", "burst", "splash", "steam" }[kind]);
        Setting(initialize, "m_HLSLCode", InitializeCode(kind));
        Call(init, "AddChild", initialize);
        int index = 1;
        foreach (object slot in Slots(initialize, "inputSlots"))
        {
            string name = ((string)Get(slot, "name")).TrimStart('_');
            bool vector = name == "Origin" || name == "Direction" || name == "Up";
            object value = vector ? name == "Direction" ? Vector3.right : name == "Up" ? TiltedView.Up : Vector3.zero
                : name == "Range" ? 5f : name == "Width" ? (kind == 1 ? 3f : 0.6f)
                : name == "Lifetime" ? (kind == 4 ? 1.3f : kind == 3 ? 0.45f : kind == 2 ? 0.09f : 0.3f)
                : kind == 4 ? 0.45f : 0.14f;
            Property(graph, slot, name, vector ? typeof(Vector3) : typeof(float), value, index++);
        }
        object fade = New("UnityEditor.VFX.Block.CustomHLSL");
        Setting(fade, "m_BlockName", kind == 4 ? "Expand and fade steam" : "Fade droplets");
        Setting(fade, "m_HLSLCode", UpdateCode(kind));
        Call(output, "AddChild", fade);
        Call(TypeOf("UnityEditor.VFX.VisualEffectResourceExtensions"), "WriteAssetWithSubAssets", resource);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static object Node(object graph, string type, Vector2 position)
    {
        object node = New("UnityEditor.VFX." + type);
        Set(node, "position", position);
        Call(graph, "AddChild", node);
        return node;
    }

    private static void Property(object graph, object slot, string name, Type type, object value, int order)
    {
        object parameter = New("UnityEditor.VFX.VFXParameter");
        Call(parameter, "Init", type);
        Setting(parameter, "m_ExposedName", name);
        Setting(parameter, "m_Exposed", true);
        Set(parameter, "value", value);
        Set(parameter, "position", new Vector2(-350, -400 + order * 110));
        Call(graph, "AddChild", parameter);
        object output = Slots(parameter, "outputSlots").First();
        Call(slot, "Link", output);
        int id = (int)Call(parameter, "AddNode", new Vector2(-350, -400 + order * 110));
        object node = Call(parameter, "GetNode", id);
        var linkType = TypeOf("UnityEditor.VFX.VFXParameter+NodeLinkedSlot");
        object link = Activator.CreateInstance(linkType);
        linkType.GetField("inputSlot").SetValue(link, slot);
        linkType.GetField("outputSlot").SetValue(link, output);
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(linkType));
        list.Add(link);
        node.GetType().GetField("linkedSlots").SetValue(node, list);
    }

    private static string InitializeCode(int kind)
    {
        const string header = @"void InitializeHose(inout VFXAttributes attributes, in float3 Origin, in float3 Direction, in float3 Up, in float Range, in float Width, in float Lifetime, in float ParticleSize)
{
    float r = frac(sin((float(attributes.particleId) + 1.0) * 12.9898) * 43758.5453);
    float s = frac(sin((float(attributes.particleId) + 2.0) * 78.233) * 23421.631);
    float q = frac(sin((float(attributes.particleId) + 3.0) * 39.425) * 17834.345);
    float3 forward = normalize(Direction + float3(0.00001, 0, 0));
    float3 across = normalize(float3(-forward.y, forward.x, 0) + float3(0.00001, 0, 0));
    attributes.position = Origin;
    attributes.age = 0;
    attributes.lifetime = max(0.02, Lifetime);
    attributes.size = ParticleSize * lerp(0.7, 1.3, s);
    attributes.scaleX = 1;
    attributes.scaleY = 1;
    attributes.scaleZ = 1;
    attributes.color = float3(0.52, 0.82, 1.0);
    attributes.alpha = 1;
";
        string body;
        if (kind == 4)
            body = @"    attributes.position += across * (r - 0.5) * 0.45;
    attributes.velocity = Up * lerp(0.55, 1.0, s) + across * (r - 0.5) * 0.5;
    attributes.lifetime *= lerp(0.8, 1.2, q);
    attributes.color = float3(0.86, 0.91, 0.95);
";
        else if (kind == 3)
            body = @"    attributes.velocity = forward * lerp(0.6, 2.3, q) + across * (r - 0.5) * 4.0 + Up * lerp(0.3, 1.0, s);
    attributes.lifetime *= lerp(0.6, 1.0, s);
    attributes.size *= 0.65;
";
        else
            body = "    attributes.velocity = forward * (Range / max(0.02, Lifetime))" +
                (kind == 1 ? " * lerp(0.65, 1.0, q)" : "") +
                " + across * ((r - 0.5) * Width / max(0.02, Lifetime))" + (kind == 1 ? ";\n" : " * 0.25;\n") +
                "    attributes.position += across * (s - 0.5) * min(Width, 0.08);\n";
        return header + body + "}\n";
    }

    private static string UpdateCode(int kind)
    {
        return @"void FadeHose(inout VFXAttributes attributes)
{
    float t = saturate(attributes.age / max(0.02, attributes.lifetime));
" + (kind == 4
            ? "    attributes.alpha = 0.32 * smoothstep(0.0, 0.12, t) * (1.0 - t);\n    attributes.size *= 1.0 + 2.0 * t;\n"
            : "    attributes.alpha = " + (kind == 1 ? "0.32" : "0.85") + " * (1.0 - smoothstep(0.55, 1.0, t));\n") + "}\n";
    }

    private static void CreatePreviewScene(HoseVfxLibrary library)
    {
        const string path = "Assets/_Scenes/HoseVfxPreview.unity";
        if (File.Exists(path)) return;
        var previous = SceneManagerActive();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var cameraObject = new GameObject("Preview camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.backgroundColor = new Color(0.025f, 0.045f, 0.065f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 0f, -20f);
            var demo = new GameObject("Hose effects preview");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(demo, scene);
            demo.AddComponent<HoseVfxPreview>().SetLibrary(library);
            EditorSceneManager.SaveScene(scene, path);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        }
    }

    private static UnityEngine.SceneManagement.Scene SceneManagerActive() => UnityEngine.SceneManagement.SceneManager.GetActiveScene();

    private static Type TypeOf(string name) => Assembly.Load(name == "UnityEditor.VFX.VisualEffectResource"
            ? "UnityEditor.VFXModule" : "Unity.VisualEffectGraph.Editor").GetType(name)
        ?? throw new InvalidOperationException("VFX Graph 17.6 type unavailable: " + name);
    private static object New(string name) => ScriptableObject.CreateInstance(TypeOf(name));
    private static object Get(object target, string name) => target.GetType().GetProperty(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetProperty(name, Flags).SetValue(target, value);
    private static object[] Slots(object target, string name) => ((IEnumerable)Get(target, name)).Cast<object>().ToArray();
    private static void Setting(object target, string name, object value) => Call(target, "SetSettingValue", name, value);
    private static void SettingEnum(object target, string name, string value)
    {
        object current = Call(target, "GetSettingValue", name);
        Setting(target, name, Enum.Parse(current.GetType(), value));
    }
    private static void SetEnumProperty(object target, string name, string value)
    {
        var property = target.GetType().GetProperty(name, Flags);
        property.SetValue(target, Enum.Parse(property.PropertyType, value));
    }
    private static object Call(object target, string name, params object[] arguments)
    {
        Type type = target as Type ?? target.GetType();
        var method = type.GetMethods(Flags).Where(m => m.Name == name).First(m =>
        {
            var p = m.GetParameters();
            return p.Length >= arguments.Length && p.Skip(arguments.Length).All(a => a.IsOptional)
                && arguments.Select((a, i) => a == null || p[i].ParameterType.IsInstanceOfType(a)).All(match => match);
        });
        var parameters = method.GetParameters();
        object[] values = parameters.Select((p, i) => i < arguments.Length ? arguments[i] : p.DefaultValue).ToArray();
        return method.Invoke(target is Type ? null : target, values);
    }
}

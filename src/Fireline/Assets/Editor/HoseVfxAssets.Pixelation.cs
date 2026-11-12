using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class HoseVfxAssets
{
    [MenuItem("Fireline/VFX/Upgrade Contact Pixelation")]
    public static void UpgradeContactPixelation()
    {
        var library = AssetDatabase.LoadAssetAtPath<HoseVfxLibrary>(Root + "/HoseVfxLibrary.asset");
        if (library == null) throw new InvalidOperationException("Create the hose VFX library first.");
        if (library.GraphVersion < 5) UpgradeHoseFeelAndSmoke();
        string shaderPath = Root + "/PixelatedWater.shadergraph";
        AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceSynchronousImport);
        object shader = Call(TypeOf("UnityEditor.VFX.VFXShaderGraphHelpers"), "LoadShaderGraphAssetAtPath", shaderPath);
        if (shader == null) throw new InvalidOperationException("Pixelated water shader did not import.");
        string path = AssetDatabase.GetAssetPath(library.Steam);
        var model = OpenModel(path, false);
        PixelDefaults(model);
        foreach (object output in ((IEnumerable)Get(model.Graph, "children")).Cast<object>()
            .Where(c => c.GetType().Name == "VFXPlanarPrimitiveOutput" || c.GetType().Name == "VFXComposedParticleOutput").ToArray())
        {
            object replacement = output;
            if (output.GetType().Name != "VFXComposedParticleOutput")
            {
                var links = new List<(object block, string[] path, object value, object[] sources)>();
                foreach (object block in ((IEnumerable)Get(output, "children")))
                foreach (object slot in Slots(block, "inputSlots")) CaptureSlotLinks(block, slot, new string[0], links);
                object texture = Get(Slots(output, "inputSlots").First(s => (string)Get(s, "name") == "mainTexture"), "value");
                object[] previous = ((IEnumerable)Get(model.Graph, "children")).Cast<object>().ToArray();
                Setting(output, "shaderGraph", shader);
                Call(TypeOf("UnityEditor.VFX.SanitizeHelper"), "MigrateSGOutputToComposed", output);
                replacement = ((IEnumerable)Get(model.Graph, "children")).Cast<object>()
                    .Single(c => !previous.Contains(c) && c.GetType().Name == "VFXComposedParticleOutput");
                Set(Slots(replacement, "inputSlots").First(s => (string)Get(s, "name") == "_BaseMap"), "value", texture);
                foreach (object block in ((IEnumerable)Get(replacement, "children"))) Call(block, "ResyncSlots", true);
                foreach (var link in links.Where(l => l.path.Length == 1)) Set(FindBlockSlot(link.block, link.path), "value", link.value);
                foreach (var link in links)
                foreach (object source in link.sources)
                    if (!(bool)Call(FindBlockSlot(link.block, link.path), "Link", source))
                        throw new InvalidOperationException("Could not preserve a steam output block input.");
            }
            BindParticleTint(model, replacement);
            BindPixelInputs(model, replacement);
        }
        SaveModel(model, path);
        CheckProperties(library.Steam, PixelProperties);
        CheckProperties(library.Splash, PixelProperties);
        Debug.Log("[Hose VFX] Independent splash and steam pixelation ready.");
    }

    [MenuItem("Fireline/VFX/Upgrade Water Pixelation and Contact Splash")]
    public static void UpgradeWaterPixelation()
    {
        var library = AssetDatabase.LoadAssetAtPath<HoseVfxLibrary>(Root + "/HoseVfxLibrary.asset");
        if (library == null) throw new InvalidOperationException("Create the hose VFX library first.");
        if (library.GraphVersion < 3) UpgradeStreamMotion();
        if (library.GraphVersion < 4)
        {
            string shaderPath = Root + "/PixelatedWater.shadergraph";
            AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            object shader = Call(TypeOf("UnityEditor.VFX.VFXShaderGraphHelpers"), "LoadShaderGraphAssetAtPath", shaderPath);
            if (shader == null) throw new InvalidOperationException("Water Shader Graph did not import.");
            // Change only output shading. Keep the existing simulation, textures, capacities,
            // artist-authored blocks, and all graph GUIDs.
            foreach (var asset in new[] { library.Stream, library.Mist, library.Burst, library.Splash })
            {
                string path = AssetDatabase.GetAssetPath(asset);
                var model = OpenModel(path, false);
                model.Defaults["PixelSize"] = 4f;
                var outputs = ((IEnumerable)Get(model.Graph, "children")).Cast<object>()
                    .Where(c => c.GetType().Name == "VFXMeshOutput" || c.GetType().Name == "VFXPlanarPrimitiveOutput"
                        || c.GetType().Name == "VFXComposedParticleOutput").ToArray();
                foreach (object output in outputs)
                {
                    object replacement = output;
                    if (output.GetType().Name != "VFXComposedParticleOutput")
                    {
                        var links = new List<(object block, string[] path, object value, object[] sources)>();
                        foreach (object block in ((IEnumerable)Get(output, "children")))
                        foreach (object slot in Slots(block, "inputSlots")) CaptureSlotLinks(block, slot, new string[0], links);
                        object texture = Get(Slots(output, "inputSlots").First(s => (string)Get(s, "name") == "mainTexture"), "value");
                        object[] previous = ((IEnumerable)Get(model.Graph, "children")).Cast<object>().ToArray();
                        Setting(output, "shaderGraph", shader);
                        Call(TypeOf("UnityEditor.VFX.SanitizeHelper"), "MigrateSGOutputToComposed", output);
                        replacement = ((IEnumerable)Get(model.Graph, "children")).Cast<object>()
                            .Single(c => !previous.Contains(c) && c.GetType().Name == "VFXComposedParticleOutput");
                        Set(Slots(replacement, "inputSlots").First(s => (string)Get(s, "name") == "_BaseMap"), "value", texture);
                        Bind(model, Slots(replacement, "inputSlots").First(s => (string)Get(s, "name") == "PixelSize"), "PixelSize");
                        // Unity's output conversion can remove the transferred block's slots.
                        // Recreate them and restore values and exact sources by their slot paths.
                        foreach (object block in ((IEnumerable)Get(replacement, "children"))) Call(block, "ResyncSlots", true);
                        foreach (var link in links.Where(l => l.path.Length == 1))
                            Set(FindBlockSlot(link.block, link.path), "value", link.value);
                        foreach (var link in links)
                        foreach (object source in link.sources)
                            if (!(bool)Call(FindBlockSlot(link.block, link.path), "Link", source))
                                throw new InvalidOperationException("Could not preserve an output block input.");
                    }
                    else
                    {
                        // Resume an interrupted upgrade whose output has already been converted.
                        foreach (object block in ((IEnumerable)Get(replacement, "children")).Cast<object>()
                            .Where(b => b.GetType().Name == "CustomHLSL"))
                        {
                            Call(block, "ResyncSlots", true);
                            foreach (object slot in Slots(block, "inputSlots"))
                            {
                                string name = ((string)Get(slot, "name")).TrimStart('_');
                                if (!(bool)Call(slot, "HasLink", true) && model.Parameters.ContainsKey(name)) Bind(model, slot, name);
                            }
                        }
                    }
                    BindParticleTint(model, replacement);
                }
                if (asset == library.Splash && !model.Parameters.ContainsKey("ContactRate"))
                {
                    model.Defaults["ContactRate"] = 0f;
                    object initialize = ((IEnumerable)Get(model.Graph, "children")).Cast<object>()
                        .Single(c => c.GetType().Name == "VFXBasicInitialize");
                    object continuous = Node(model.Graph, "VFXBasicSpawner", new Vector2(700, -400));
                    Set(continuous, "label", "Continuous beam contact");
                    Call(initialize, "LinkFrom", continuous);
                    object rate = New("UnityEditor.VFX.VFXSpawnerConstantRate");
                    Call(continuous, "AddChild", rate);
                    Bind(model, Slots(rate, "inputSlots").First(), "ContactRate");
                }
                SaveModel(model, path);
            }
            var serialized = new SerializedObject(library);
            serialized.FindProperty("graphVersion").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
        // Also repair partially imported version-four output blocks. A valid authored
        // block has its input slots already, so repeat upgrades leave artist edits alone.
        foreach (var asset in new[] { library.Stream, library.Mist, library.Burst, library.Splash })
        {
            var model = OpenModel(AssetDatabase.GetAssetPath(asset), false);
            bool repaired = false;
            foreach (object output in ((IEnumerable)Get(model.Graph, "children")).Cast<object>()
                .Where(c => c.GetType().Name == "VFXComposedParticleOutput"))
            foreach (object block in ((IEnumerable)Get(output, "children")).Cast<object>()
                .Where(b => b.GetType().Name == "CustomHLSL" && Slots(b, "inputSlots").Length == 0))
            {
                Call(block, "ResyncSlots", true);
                foreach (object slot in Slots(block, "inputSlots"))
                {
                    string name = ((string)Get(slot, "name")).TrimStart('_');
                    if (model.Parameters.ContainsKey(name)) { Bind(model, slot, name); repaired = true; }
                }
            }
            if (repaired) SaveModel(model, AssetDatabase.GetAssetPath(asset));
        }
        foreach (var asset in new[] { library.Stream, library.Mist, library.Burst, library.Splash })
            CheckProperties(asset, "PixelSize");
        CheckProperties(library.Splash, "Count", "ContactRate");
        Debug.Log("[Hose VFX] Water pixelation and continuous beam splash ready. Existing water motion and soft steam preserved.");
    }

    private static void BindParticleTint(GraphModel model, object output)
    {
        object target = Slots(output, "inputSlots").First(s => (string)Get(s, "name") == "_Color");
        if ((bool)Call(target, "HasLink", true)) return;
        // Shader Graph inputs need explicit per-particle color/alpha. A white constant
        // would discard the original mist transparency and the beam's release fade.
        object color = New("UnityEditor.VFX.VFXAttributeParameter");
        Setting(color, "attribute", "color");
        Call(model.Graph, "AddChild", color);
        object alpha = New("UnityEditor.VFX.VFXAttributeParameter");
        Setting(alpha, "attribute", "alpha");
        Call(model.Graph, "AddChild", alpha);
        object[] components = Slots(target, "children");
        object[] rgb = Slots(Slots(color, "outputSlots").First(), "children");
        for (int i = 0; i < 3; i++)
            if (!(bool)Call(components[i], "Link", rgb[i])) throw new InvalidOperationException("Could not bind water color.");
        if (!(bool)Call(components[3], "Link", Slots(alpha, "outputSlots").First()))
            throw new InvalidOperationException("Could not bind water alpha.");
    }

    private static void CaptureSlotLinks(object block, object slot, string[] parentPath,
        List<(object block, string[] path, object value, object[] sources)> links)
    {
        string[] path = parentPath.Concat(new[] { (string)Get(slot, "name") }).ToArray();
        links.Add((block, path, Get(slot, "value"), ((IEnumerable)Get(slot, "LinkedSlots")).Cast<object>().ToArray()));
        foreach (object child in Slots(slot, "children")) CaptureSlotLinks(block, child, path, links);
    }

    private static object FindBlockSlot(object block, string[] path)
    {
        object slot = Slots(block, "inputSlots").Single(s => (string)Get(s, "name") == path[0]);
        foreach (string name in path.Skip(1)) slot = Slots(slot, "children").Single(s => (string)Get(s, "name") == name);
        return slot;
    }
}

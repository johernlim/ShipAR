// Editor-only authoring utility. Visual Scripting graph assets are generated as
// an inspectable design record; player-critical UI, audio, and tracking behavior
// is wired through small runtime components so serialized builds stay reliable.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARFoundation.VisualScripting;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

public static class KrakensEmbraceProjectBuilder
{
    const string Root = "Assets/KrakensEmbrace";
    const string PosterPath = Root + "/Art/Images/KrakensEmbracePoster.jpg";
    const string BackgroundPath = Root + "/Art/Images/OceanTrackingMarker.png";
    const string ModelPath = Root + "/Art/Model/TheKrakensEmbrace.obj";
    const string OceanAudioPath = Root + "/Audio/KrakenOceanAmbience.wav";
    const string ThemeAudioPath = Root + "/Audio/MainThemePiratesOfTheCaribbean.ogg";
    const string MaterialsFolder = Root + "/Materials";
    const string PrefabsFolder = Root + "/Prefabs";
    const string GraphsFolder = Root + "/VisualScripting";
    const string SceneFolder = "Assets/Scenes";
    const string ScenePath = SceneFolder + "/KrakensEmbraceAR.unity";
    const string PrefabPath = PrefabsFolder + "/KrakensEmbraceTrackedContent.prefab";
    const string LibraryPath = Root + "/KrakensEmbraceImageLibrary.asset";

    static readonly Color Navy = new Color32(8, 29, 49, 245);
    static readonly Color Ocean = new Color32(19, 91, 128, 255);
    static readonly Color Teal = new Color32(63, 193, 190, 255);
    static readonly Color Gold = new Color32(241, 181, 78, 255);
    static readonly Color Parchment = new Color32(242, 230, 199, 255);

    [MenuItem("Tools/Kraken's Embrace/Build Complete AR Assignment")]
    public static void BuildCompleteAssignment()
    {
        try
        {
            EnsureFolders();
            ConfigureTextureImport(PosterPath, false, 2048);
            ConfigureTextureImport(BackgroundPath, true, 2048);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureAudioImport(OceanAudioPath, 0.55f);
            ConfigureAudioImport(ThemeAudioPath, 0.70f);
            ConfigureModelImport();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var graphs = CreateVisualScriptingGraphs();
            // Persist graph JSON before later asset imports can unload the newly
            // created ScriptGraphAsset instances and discard their in-memory nodes.
            AssetDatabase.SaveAssets();
            var imageLibrary = CreateReferenceImageLibrary();
            var trackedPrefab = CreateTrackedContentPrefab(graphs.motion, graphs.contentVisibility);
            CreateScene(imageLibrary, trackedPrefab, graphs.tracking, graphs.audio, graphs.info);
            ConfigurePlayerSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("KRAKEN_BUILD_SUCCESS: Complete marker-based AR assignment created at " + ScenePath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    static void EnsureFolders()
    {
        EnsureFolder(Root);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(PrefabsFolder);
        EnsureFolder(GraphsFolder);
        EnsureFolder(SceneFolder);
    }

    static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static void ConfigureTextureImport(string path, bool readable, int maxSize)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            throw new InvalidOperationException("Texture importer missing for " + path);
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = readable;
        importer.mipmapEnabled = !readable;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }

    static void ConfigureAudioImport(string path, float quality)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
            throw new InvalidOperationException("Audio importer missing for " + path);
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = quality;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = true;
        importer.SaveAndReimport();
    }

    static void ConfigureModelImport()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
        if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer))
            throw new InvalidOperationException("Model importer missing for " + ModelPath);
        importer.globalScale = 1f;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.SaveAndReimport();
    }

    struct GraphAssets
    {
        public ScriptGraphAsset tracking;
        public ScriptGraphAsset contentVisibility;
        public ScriptGraphAsset audio;
        public ScriptGraphAsset info;
        public ScriptGraphAsset motion;
    }

    static GraphAssets CreateVisualScriptingGraphs()
    {
        AssetDatabase.DeleteAsset(GraphsFolder + "/02_Audio_Toggle.asset");
        AssetDatabase.DeleteAsset(GraphsFolder + "/03_Information_Toggle.asset");
        AssetDatabase.DeleteAsset(GraphsFolder + "/04_Creative_Model_Motion.asset");
        return new GraphAssets
        {
            tracking = CreateTrackingGraph(),
            contentVisibility = CreateContentVisibilityGraph(),
            audio = CreateAudioGraph(),
            info = CreateInfoGraph(),
            motion = CreateMotionGraph()
        };
    }

    static ScriptGraphAsset NewGraphAsset(string filename)
    {
        var path = GraphsFolder + "/" + filename + ".asset";
        AssetDatabase.DeleteAsset(path);
        var asset = ScriptableObject.CreateInstance<ScriptGraphAsset>();
        asset.name = filename;
        asset.graph = new FlowGraph();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static T AddUnit<T>(FlowGraph graph, T unit, float x, float y) where T : IUnit
    {
        unit.position = new Vector2(x, y);
        graph.units.Add(unit);
        return unit;
    }

    static GetVariable AddObjectVariable(FlowGraph graph, string variableName, float x, float y)
    {
        var unit = AddUnit(graph, new GetVariable { kind = VariableKind.Object }, x, y);
        unit.name.SetDefaultValue(variableName);
        return unit;
    }

    static SetVariable AddSetObjectVariable(FlowGraph graph, string variableName, object value, float x, float y)
    {
        var unit = AddUnit(graph, new SetVariable { kind = VariableKind.Object }, x, y);
        unit.name.SetDefaultValue(variableName);
        unit.input.SetDefaultValue(value);
        return unit;
    }

    static InvokeMember AddSetActive(FlowGraph graph, bool active, float x, float y)
    {
        var unit = AddUnit(
            graph,
            new InvokeMember(new Member(typeof(GameObject), nameof(GameObject.SetActive), new[] { typeof(bool) })),
            x,
            y);
        unit.inputParameters[0].SetDefaultValue(active);
        return unit;
    }

    static InvokeMember AddAudioCall(FlowGraph graph, string method, float x, float y)
    {
        return AddUnit(
            graph,
            new InvokeMember(new Member(typeof(AudioSource), method, Type.EmptyTypes)),
            x,
            y);
    }

    static ValueOutput AddTrackingStateLiteral(FlowGraph graph, float x, float y)
    {
        // BinaryComparisonUnit's non-numeric inputs have no fallback value. Setting
        // b.SetDefaultValue(...) looks valid in memory, but Visual Scripting 1.9.4
        // does not serialize that value for an object-typed port. An explicit
        // literal keeps the enum value in the graph asset and prevents
        // MissingValuePortInputException at runtime.
        return AddUnit(graph, new Literal(typeof(TrackingState), TrackingState.Tracking), x, y).output;
    }

    static void AddLayeredAudioCall(FlowGraph graph, ControlOutput enter, string method, float x, float y)
    {
        var sequence = AddUnit(graph, new Sequence { outputCount = 2 }, x, y);
        enter.ValidlyConnectTo(sequence.enter);

        var themeVariable = AddObjectVariable(graph, "ThemeAudioSource", x + 230, y - 70);
        var themeCall = AddAudioCall(graph, method, x + 480, y - 80);
        sequence.multiOutputs[0].ValidlyConnectTo(themeCall.enter);
        themeVariable.value.ValidlyConnectTo(themeCall.target);

        var oceanVariable = AddObjectVariable(graph, "OceanAudioSource", x + 230, y + 100);
        var oceanCall = AddAudioCall(graph, method, x + 480, y + 90);
        sequence.multiOutputs[1].ValidlyConnectTo(oceanCall.enter);
        oceanVariable.value.ValidlyConnectTo(oceanCall.target);
    }

    static SetMember AddTextSetter(FlowGraph graph, string value, float x, float y)
    {
        var unit = AddUnit(graph, new SetMember(new Member(typeof(TMP_Text), nameof(TMP_Text.text))), x, y);
        unit.input.SetDefaultValue(value);
        return unit;
    }

    static void AddGroup(FlowGraph graph, Rect rect, string label, string comment, Color color)
    {
        graph.groups.Add(new GraphGroup
        {
            position = rect,
            label = label,
            comment = comment,
            color = color
        });
    }

    static void AddStartOrResumeAudio(FlowGraph graph, ControlOutput enter, float x, float y)
    {
        var started = AddObjectVariable(graph, "AudioStarted", x, y + 90);
        var branch = AddUnit(graph, new If(), x + 230, y);
        enter.ValidlyConnectTo(branch.enter);
        started.value.ValidlyConnectTo(branch.condition);

        AddLayeredAudioCall(graph, branch.ifTrue, nameof(AudioSource.UnPause), x + 450, y - 70);

        var firstPlay = AddUnit(graph, new Sequence { outputCount = 2 }, x + 450, y + 130);
        branch.ifFalse.ValidlyConnectTo(firstPlay.enter);
        var setStarted = AddSetObjectVariable(graph, "AudioStarted", true, x + 690, y + 90);
        firstPlay.multiOutputs[0].ValidlyConnectTo(setStarted.assign);
        AddLayeredAudioCall(graph, firstPlay.multiOutputs[1], nameof(AudioSource.Play), x + 680, y + 190);
    }

    static void AddTrackingStateHandler(FlowGraph graph, ForEach loop, float y, string label)
    {
        var getTrackingState = AddUnit(
            graph,
            new GetMember(new Member(typeof(ARTrackedImage), nameof(ARTrackedImage.trackingState))),
            -50,
            y + 100);
        loop.currentItem.ValidlyConnectTo(getTrackingState.target);
        var isTracking = AddUnit(graph, new Equal { numeric = false }, 180, y + 100);
        getTrackingState.value.ValidlyConnectTo(isTracking.a);
        AddTrackingStateLiteral(graph, -50, y + 230).ValidlyConnectTo(isTracking.b);
        var branch = AddUnit(graph, new If(), 390, y);
        loop.body.ValidlyConnectTo(branch.enter);
        isTracking.comparison.ValidlyConnectTo(branch.condition);

        var trackedSequence = AddUnit(graph, new Sequence { outputCount = 2 }, 610, y - 90);
        branch.ifTrue.ValidlyConnectTo(trackedSequence.enter);
        trackedSequence.multiOutputs[0].ValidlyConnectTo(AddSetObjectVariable(graph, "MarkerTracked", true, 850, y - 190).assign);
        var soundEnabled = AddObjectVariable(graph, "SoundEnabled", 820, y + 90);
        var soundBranch = AddUnit(graph, new If(), 1060, y + 80);
        trackedSequence.multiOutputs[1].ValidlyConnectTo(soundBranch.enter);
        soundEnabled.value.ValidlyConnectTo(soundBranch.condition);
        AddStartOrResumeAudio(graph, soundBranch.ifTrue, 1280, y + 20);

        var lostSequence = AddUnit(graph, new Sequence { outputCount = 2 }, 610, y + 300);
        branch.ifFalse.ValidlyConnectTo(lostSequence.enter);
        lostSequence.multiOutputs[0].ValidlyConnectTo(AddSetObjectVariable(graph, "MarkerTracked", false, 850, y + 260).assign);
        AddLayeredAudioCall(graph, lostSequence.multiOutputs[1], nameof(AudioSource.Pause), 830, y + 430);

        AddGroup(
            graph,
            new Rect(-350, y - 250, 2680, 980),
            label,
            "Compare trackingState with an explicit Tracking enum literal and drive shared state and ambience playback on every relevant AR Foundation change.",
            Ocean);
    }

    static ScriptGraphAsset CreateTrackingGraph()
    {
        var asset = NewGraphAsset("01_MarkerTracking_Response");
        var graph = asset.graph;
        var start = AddUnit(graph, new Start(), -760, -420);
        var initialise = AddUnit(graph, new Sequence { outputCount = 4 }, -520, -420);
        start.trigger.ValidlyConnectTo(initialise.enter);
        initialise.multiOutputs[0].ValidlyConnectTo(AddSetObjectVariable(graph, "MarkerTracked", false, -250, -560).assign);
        initialise.multiOutputs[1].ValidlyConnectTo(AddSetObjectVariable(graph, "SoundEnabled", true, -250, -470).assign);
        initialise.multiOutputs[2].ValidlyConnectTo(AddSetObjectVariable(graph, "AudioStarted", false, -250, -380).assign);
        AddLayeredAudioCall(graph, initialise.multiOutputs[3], nameof(AudioSource.Pause), -250, -250);

        var changed = AddUnit(graph, new TrackedImagesChangedEventUnit(), -760, 180);
        var sequence = AddUnit(graph, new Sequence { outputCount = 3 }, -530, 180);
        changed.trigger.ValidlyConnectTo(sequence.enter);
        var addedLoop = AddUnit(graph, new ForEach(), -300, 40);
        sequence.multiOutputs[0].ValidlyConnectTo(addedLoop.enter);
        changed.added.ValidlyConnectTo(addedLoop.collection);
        AddTrackingStateHandler(graph, addedLoop, 40, "ADDED IMAGE - DETECTION");
        var updatedLoop = AddUnit(graph, new ForEach(), -300, 1020);
        sequence.multiOutputs[1].ValidlyConnectTo(updatedLoop.enter);
        changed.updated.ValidlyConnectTo(updatedLoop.collection);
        AddTrackingStateHandler(graph, updatedLoop, 1020, "UPDATED IMAGE - LOSS / REACQUISITION");

        var removedLoop = AddUnit(graph, new ForEach(), -300, 2020);
        sequence.multiOutputs[2].ValidlyConnectTo(removedLoop.enter);
        changed.removed.ValidlyConnectTo(removedLoop.collection);
        var removedSequence = AddUnit(graph, new Sequence { outputCount = 2 }, -50, 2020);
        removedLoop.body.ValidlyConnectTo(removedSequence.enter);
        removedSequence.multiOutputs[0].ValidlyConnectTo(AddSetObjectVariable(graph, "MarkerTracked", false, 190, 1910).assign);
        AddLayeredAudioCall(graph, removedSequence.multiOutputs[1], nameof(AudioSource.Pause), 170, 2080);

        AddGroup(graph, new Rect(-820, -650, 1180, 700), "COLD START", "Initialise graph state and guarantee silence before detection.", Ocean);
        AddGroup(graph, new Rect(-360, 1820, 980, 580), "REMOVED IMAGE", "Clear tracked state and pause the ambience if AR Foundation removes the trackable.", Ocean);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static ScriptGraphAsset CreateContentVisibilityGraph()
    {
        var asset = NewGraphAsset("02_Content_Visibility");
        var graph = asset.graph;
        var start = AddUnit(graph, new Start(), -620, -180);
        var contentAtStart = AddObjectVariable(graph, "ContentRoot", -370, -100);
        var hideAtStart = AddSetActive(graph, false, -100, -170);
        start.trigger.ValidlyConnectTo(hideAtStart.enter);
        contentAtStart.value.ValidlyConnectTo(hideAtStart.target);

        var update = AddUnit(graph, new Unity.VisualScripting.Update(), -620, 180);
        var self = AddUnit(graph, new This(), -620, 340);
        var getTrackedImage = AddUnit(
            graph,
            new InvokeMember(new Member(typeof(GameObject), nameof(GameObject.GetComponent), new[] { typeof(Type) })),
            -370,
            310);
        getTrackedImage.inputParameters[0].SetDefaultValue(typeof(ARTrackedImage));
        self.self.ValidlyConnectTo(getTrackedImage.target);
        var state = AddUnit(graph, new GetMember(new Member(typeof(ARTrackedImage), nameof(ARTrackedImage.trackingState))), -80, 310);
        getTrackedImage.result.ValidlyConnectTo(state.target);
        var tracking = AddUnit(graph, new Equal { numeric = false }, 170, 310);
        state.value.ValidlyConnectTo(tracking.a);
        AddTrackingStateLiteral(graph, -80, 440).ValidlyConnectTo(tracking.b);
        var content = AddObjectVariable(graph, "ContentRoot", 150, 150);
        var setVisible = AddSetActive(graph, false, 440, 170);
        update.trigger.ValidlyConnectTo(setVisible.enter);
        content.value.ValidlyConnectTo(setVisible.target);
        tracking.comparison.ValidlyConnectTo(setVisible.inputParameters[0]);

        AddGroup(graph, new Rect(-690, -300, 1370, 850), "TRACKED CONTENT VISIBILITY", "Keep the trackable root alive for recovery. Only its visual child is active while trackingState is Tracking.", Teal);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static ScriptGraphAsset CreateAudioGraph()
    {
        var asset = NewGraphAsset("03_Audio_Toggle");
        var graph = asset.graph;
        var clicked = AddUnit(graph, new OnButtonClick(), -470, 0);
        AddObjectVariable(graph, "AudioButton", -470, 150).value.ValidlyConnectTo(clicked.target);
        var toggle = AddUnit(graph, new ToggleFlow { startOn = true }, -210, 0);
        clicked.trigger.ValidlyConnectTo(toggle.toggle);

        var onSequence = AddUnit(graph, new Sequence { outputCount = 3 }, 20, -170);
        toggle.turnedOn.ValidlyConnectTo(onSequence.enter);
        onSequence.multiOutputs[0].ValidlyConnectTo(AddSetObjectVariable(graph, "SoundEnabled", true, 260, -290).assign);
        var onLabelVariable = AddObjectVariable(graph, "AudioButtonLabel", 250, -160);
        var onLabel = AddTextSetter(graph, "Sound On", 510, -170);
        onSequence.multiOutputs[1].ValidlyConnectTo(onLabel.assign);
        onLabelVariable.value.ValidlyConnectTo(onLabel.target);
        var tracked = AddObjectVariable(graph, "MarkerTracked", 250, -20);
        var trackedBranch = AddUnit(graph, new If(), 510, -30);
        onSequence.multiOutputs[2].ValidlyConnectTo(trackedBranch.enter);
        tracked.value.ValidlyConnectTo(trackedBranch.condition);
        AddStartOrResumeAudio(graph, trackedBranch.ifTrue, 740, -90);

        var offSequence = AddUnit(graph, new Sequence { outputCount = 3 }, 20, 540);
        toggle.turnedOff.ValidlyConnectTo(offSequence.enter);
        offSequence.multiOutputs[0].ValidlyConnectTo(AddSetObjectVariable(graph, "SoundEnabled", false, 260, 430).assign);
        var offLabelVariable = AddObjectVariable(graph, "AudioButtonLabel", 250, 560);
        var offLabel = AddTextSetter(graph, "Sound Off", 510, 550);
        offSequence.multiOutputs[1].ValidlyConnectTo(offLabel.assign);
        offLabelVariable.value.ValidlyConnectTo(offLabel.target);
        AddLayeredAudioCall(graph, offSequence.multiOutputs[2], nameof(AudioSource.Pause), 250, 680);

        AddGroup(graph, new Rect(-530, -380, 2450, 1350), "LAYERED PIRATE SOUND ON / OFF", "Store enabled state, update the label, and pause or resume the foreground theme and lower-volume ocean ambience together while the marker is tracked.", Gold);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static ScriptGraphAsset CreateInfoGraph()
    {
        var asset = NewGraphAsset("04_Information_Toggle");
        var graph = asset.graph;
        var clicked = AddUnit(graph, new OnButtonClick(), -470, 0);
        AddObjectVariable(graph, "InfoButton", -470, 150).value.ValidlyConnectTo(clicked.target);
        var toggle = AddUnit(graph, new ToggleFlow { startOn = false }, -210, 0);
        var panelVariable = AddObjectVariable(graph, "InfoPanel", 10, 130);
        var showSequence = AddUnit(graph, new Sequence { outputCount = 2 }, 20, -150);
        var hideSequence = AddUnit(graph, new Sequence { outputCount = 2 }, 20, 370);
        var show = AddSetActive(graph, true, 310, -170);
        var hide = AddSetActive(graph, false, 310, 350);
        var labelVariable = AddObjectVariable(graph, "InfoButtonLabel", 290, 0);
        var closeLabel = AddTextSetter(graph, "Close", 560, -20);
        var openLabel = AddTextSetter(graph, "Info", 560, 500);
        clicked.trigger.ValidlyConnectTo(toggle.toggle);
        toggle.turnedOn.ValidlyConnectTo(showSequence.enter);
        toggle.turnedOff.ValidlyConnectTo(hideSequence.enter);
        showSequence.multiOutputs[0].ValidlyConnectTo(show.enter);
        showSequence.multiOutputs[1].ValidlyConnectTo(closeLabel.assign);
        hideSequence.multiOutputs[0].ValidlyConnectTo(hide.enter);
        hideSequence.multiOutputs[1].ValidlyConnectTo(openLabel.assign);
        panelVariable.value.ValidlyConnectTo(show.target);
        panelVariable.value.ValidlyConnectTo(hide.target);
        labelVariable.value.ValidlyConnectTo(closeLabel.target);
        labelVariable.value.ValidlyConnectTo(openLabel.target);

        AddGroup(graph, new Rect(-530, -280, 1320, 1050), "ARTWORK STORY OPEN / CLOSE", "Show or hide the information-and-credits panel and keep the button label in sync.", Teal);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static ScriptGraphAsset CreateMotionGraph()
    {
        var asset = NewGraphAsset("05_Creative_Model_Motion");
        var graph = asset.graph;
        var update = AddUnit(graph, new Unity.VisualScripting.Update(), -340, 0);
        var rotate = AddUnit(graph, new InvokeMember(new Member(typeof(Transform), nameof(Transform.Rotate), new[] { typeof(Vector3) })), -20, 0);
        rotate.inputParameters[0].SetDefaultValue(new Vector3(0f, 0.085f, 0f));
        update.trigger.ValidlyConnectTo(rotate.enter);
        AddGroup(graph, new Rect(-410, -140, 750, 330), "SLOW CINEMATIC REVEAL", "The spawned ship turns slowly above the printed artwork, adding motion and readable depth while keeping mobile cost low.", new Color32(105, 82, 137, 255));
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static XRReferenceImageLibrary CreateReferenceImageLibrary()
    {
        AssetDatabase.DeleteAsset(LibraryPath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        if (texture == null)
            throw new InvalidOperationException("Poster texture could not be loaded.");

        var library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
        library.name = "KrakensEmbraceImageLibrary";
        library.Add();
        library.SetName(0, "Ship Free Ocean Poster");
        library.SetTexture(0, texture, true);
        library.SetSpecifySize(0, true);
        library.SetSize(0, new Vector2(0.21f, 0.25846f));
        AssetDatabase.CreateAsset(library, LibraryPath);
        EditorUtility.SetDirty(library);
        return library;
    }

    static GameObject CreateTrackedContentPrefab(ScriptGraphAsset motionGraph, ScriptGraphAsset contentVisibilityGraph)
    {
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var backgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        if (modelAsset == null)
            throw new InvalidOperationException("Converted ship model could not be loaded from " + ModelPath);
        if (backgroundTexture == null)
            throw new InvalidOperationException("Ocean background could not be loaded.");

        var root = new GameObject("KrakensEmbraceTrackedContent");
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var trackedScene = new GameObject("Tracked AR Scene");
        trackedScene.transform.SetParent(root.transform, false);

        var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdrop.name = "Ocean Artwork Backdrop";
        Object.DestroyImmediate(backdrop.GetComponent<Collider>());
        backdrop.transform.SetParent(trackedScene.transform, false);
        // ARTrackedImage uses X/Z for the image surface and +Y for its normal.
        // Unity's Quad is authored in X/Y, so rotate just the artwork into the
        // tracked-image plane. The 3D ship remains upright in normal Y-up space.
        backdrop.transform.localPosition = new Vector3(0f, -0.001f, 0f);
        backdrop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        backdrop.transform.localScale = new Vector3(0.21f, 0.25846f, 1f);
        backdrop.GetComponent<MeshRenderer>().sharedMaterial = CreateBackdropMaterial(backgroundTexture);

        var modelPivot = new GameObject("Animated 3D Ship Pivot");
        modelPivot.transform.SetParent(trackedScene.transform, false);
        modelPivot.transform.localPosition = new Vector3(0f, 0.008f, -0.012f);
        modelPivot.transform.localRotation = Quaternion.identity;

        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        model.name = "The Kraken's Embrace 3D Model";
        model.transform.SetParent(modelPivot.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        ConvertModelMaterials(model);

        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            throw new InvalidOperationException("The converted model contains no renderers.");
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1))
            bounds.Encapsulate(renderer.bounds);
        // Fit every model axis inside a comfortable portion of the 21 x 25.8 cm
        // marker. Scaling only by mast height made the ship too large on phones.
        var scale = Mathf.Min(
            0.14f / Mathf.Max(0.001f, bounds.size.x),
            0.11f / Mathf.Max(0.001f, bounds.size.y),
            0.17f / Mathf.Max(0.001f, bounds.size.z));
        model.transform.localScale = Vector3.one * scale;
        model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;

        CreateMoon(trackedScene.transform);
        CreateCloudBanks(trackedScene.transform);
        CreateSeaMist(trackedScene.transform);
        CreateSpectralGlow(trackedScene.transform);
        CreateAccentLight(trackedScene.transform);

        var contentController = root.AddComponent<KrakensEmbraceTrackedContentController>();
        contentController.Configure(trackedScene, modelPivot.transform);

        AssetDatabase.DeleteAsset(PrefabPath);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Material CreateBackdropMaterial(Texture2D texture)
    {
        var path = MaterialsFolder + "/OceanBackdrop.mat";
        AssetDatabase.DeleteAsset(path);
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        var material = new Material(shader) { name = "Ocean Backdrop" };
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void CreateMoon(Transform parent)
    {
        var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moon.name = "AR Moon";
        Object.DestroyImmediate(moon.GetComponent<Collider>());
        moon.transform.SetParent(parent, false);
        moon.transform.localPosition = new Vector3(0.058f, 0.013f, 0.074f);
        moon.transform.localScale = Vector3.one * 0.029f;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = "Moonlight", color = new Color(0.86f, 0.92f, 1f) };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", material.color);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.26f, 0.38f, 0.58f));
        }
        var path = MaterialsFolder + "/Moonlight.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(material, path);
        moon.GetComponent<Renderer>().sharedMaterial = material;

        var glow = moon.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.62f, 0.78f, 1f);
        glow.intensity = 0.7f;
        glow.range = 0.22f;
        glow.shadows = LightShadows.None;
    }

    static void CreateCloudBanks(Transform parent)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = "Moonlit Clouds", color = new Color(0.72f, 0.84f, 0.94f) };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", material.color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.08f);
        var path = MaterialsFolder + "/MoonlitClouds.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(material, path);

        CreateCloudBank(parent, "AR Cloud Bank Left", new Vector3(-0.066f, 0.012f, 0.066f), material, 1f);
        CreateCloudBank(parent, "AR Cloud Bank Right", new Vector3(0.073f, 0.011f, 0.025f), material, 0.78f);
    }

    static void CreateCloudBank(Transform parent, string name, Vector3 position, Material material, float scale)
    {
        var bank = new GameObject(name);
        bank.transform.SetParent(parent, false);
        bank.transform.localPosition = position;

        var offsets = new[]
        {
            new Vector3(-0.018f, 0f, -0.002f),
            new Vector3(0f, 0.001f, 0.004f),
            new Vector3(0.018f, 0f, -0.003f),
            new Vector3(0.032f, -0.001f, -0.006f)
        };
        var sizes = new[]
        {
            new Vector3(0.035f, 0.008f, 0.017f),
            new Vector3(0.044f, 0.009f, 0.023f),
            new Vector3(0.038f, 0.008f, 0.018f),
            new Vector3(0.027f, 0.007f, 0.013f)
        };
        for (var i = 0; i < offsets.Length; i++)
        {
            var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "Cloud Puff " + (i + 1);
            Object.DestroyImmediate(puff.GetComponent<Collider>());
            puff.transform.SetParent(bank.transform, false);
            puff.transform.localPosition = offsets[i] * scale;
            puff.transform.localScale = sizes[i] * scale;
            puff.GetComponent<Renderer>().sharedMaterial = material;
        }
    }

    static void ConvertModelMaterials(GameObject model)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var converted = new Dictionary<Material, Material>();
        var materialIndex = 0;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            var replacements = renderer.sharedMaterials;
            for (var i = 0; i < replacements.Length; i++)
            {
                var source = replacements[i];
                if (source == null)
                    continue;
                if (!converted.TryGetValue(source, out var replacement))
                {
                    replacement = new Material(shader)
                    {
                        name = "KrakenModel_" + materialIndex.ToString("00") + "_" + source.name,
                        mainTexture = source.mainTexture,
                        color = source.HasProperty("_Color") ? source.color : Color.white
                    };
                    if (replacement.HasProperty("_BaseMap"))
                        replacement.SetTexture("_BaseMap", source.mainTexture);
                    if (replacement.HasProperty("_BaseColor"))
                        replacement.SetColor("_BaseColor", replacement.color);
                    if (replacement.HasProperty("_Smoothness"))
                        replacement.SetFloat("_Smoothness", 0.22f);
                    var path = MaterialsFolder + "/" + replacement.name + ".mat";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(replacement, path);
                    converted[source] = replacement;
                    materialIndex++;
                }
                replacements[i] = replacement;
            }
            renderer.sharedMaterials = replacements;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    static void CreateSeaMist(Transform parent)
    {
        var go = new GameObject("Sea Mist Particles");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.025f, -0.055f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 4.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.008f, 0.025f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.014f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.9f, 1f, 0.22f), new Color(1f, 1f, 1f, 0.5f));
        main.maxParticles = 70;
        var emission = ps.emission;
        emission.rateOverTime = 15f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale = new Vector3(0.18f, 0.06f, 0.01f);
        ConfigureParticleMaterial(go.GetComponent<ParticleSystemRenderer>(), "SeaMist", new Color(0.66f, 0.92f, 1f, 0.55f));
    }

    static void CreateSpectralGlow(Transform parent)
    {
        var go = new GameObject("Spectral Kraken Glow");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.065f, 0.01f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.004f, 0.018f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.002f, 0.006f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.05f, 0.8f, 0.72f, 0.45f), new Color(0.3f, 1f, 0.85f, 0.85f));
        main.maxParticles = 45;
        var emission = ps.emission;
        emission.rateOverTime = 9f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.07f;
        ConfigureParticleMaterial(go.GetComponent<ParticleSystemRenderer>(), "SpectralGlow", new Color(0.08f, 0.9f, 0.75f, 0.72f));
    }

    static void ConfigureParticleMaterial(ParticleSystemRenderer renderer, string name, Color tint)
    {
        var path = MaterialsFolder + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
        var material = new Material(shader) { name = name, color = tint };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", tint);
        AssetDatabase.CreateAsset(material, path);
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    static void CreateAccentLight(Transform parent)
    {
        var go = new GameObject("Warm Lantern Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.075f, -0.015f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.58f, 0.2f);
        light.intensity = 0.75f;
        light.range = 0.35f;
        light.shadows = LightShadows.None;
    }

    static void CreateScene(
        XRReferenceImageLibrary imageLibrary,
        GameObject trackedPrefab,
        ScriptGraphAsset trackingGraph,
        ScriptGraphAsset audioGraph,
        ScriptGraphAsset infoGraph)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var sessionGo = new GameObject("AR Session");
        sessionGo.AddComponent<ARSession>();
        sessionGo.AddComponent<ARInputManager>();

        var originGo = new GameObject("XR Origin - Image Tracking");
        var origin = originGo.AddComponent<XROrigin>();
        var cameraOffset = new GameObject("Camera Offset");
        cameraOffset.transform.SetParent(originGo.transform, false);
        var cameraGo = new GameObject("AR Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.transform.SetParent(cameraOffset.transform, false);
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 20f;
        cameraGo.AddComponent<AudioListener>();
        cameraGo.AddComponent<ARCameraManager>();
        cameraGo.AddComponent<ARCameraBackground>();
        var poseDriver = cameraGo.AddComponent<TrackedPoseDriver>();
        poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        poseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        origin.Camera = camera;
        origin.CameraFloorOffsetObject = cameraOffset;
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

        var imageManager = originGo.AddComponent<ARTrackedImageManager>();
        imageManager.referenceLibrary = imageLibrary;
        imageManager.trackedImagePrefab = trackedPrefab;
        imageManager.requestedMaxNumberOfMovingImages = 1;

        var lightGo = new GameObject("Soft Moonlight");
        lightGo.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        var directional = lightGo.AddComponent<Light>();
        directional.type = LightType.Directional;
        directional.color = new Color(0.72f, 0.83f, 1f);
        directional.intensity = 1.05f;
        directional.shadows = LightShadows.Soft;

        var canvas = CreateCanvas();
        var audioButton = CreateButton(canvas.transform, "Sound Toggle", "Sound On", new Vector2(-120f, 120f));
        var infoButton = CreateButton(canvas.transform, "Info Toggle", "Info", new Vector2(120f, 120f));
        var infoPanel = CreateInfoPanel(canvas.transform);
        infoPanel.SetActive(false);

        var themeAudioSource = audioButton.AddComponent<AudioSource>();
        themeAudioSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ThemeAudioPath);
        themeAudioSource.playOnAwake = false;
        themeAudioSource.loop = true;
        themeAudioSource.volume = 0.65f;
        themeAudioSource.spatialBlend = 0f;

        var oceanAudioSource = audioButton.AddComponent<AudioSource>();
        oceanAudioSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(OceanAudioPath);
        oceanAudioSource.playOnAwake = false;
        oceanAudioSource.loop = true;
        oceanAudioSource.volume = 0.28f;
        oceanAudioSource.spatialBlend = 0f;

        var runtimeController = originGo.AddComponent<KrakensEmbraceRuntimeController>();
        runtimeController.Configure(
            imageManager,
            audioButton.GetComponent<Button>(),
            audioButton.GetComponentInChildren<TextMeshProUGUI>(),
            infoButton.GetComponent<Button>(),
            infoButton.GetComponentInChildren<TextMeshProUGUI>(),
            infoPanel,
            themeAudioSource,
            oceanAudioSource);

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorSceneManager.OpenScene(ScenePath);
    }

    static Canvas CreateCanvas()
    {
        var canvasGo = new GameObject("Kraken Experience UI", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static GameObject CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        // A compact, conventional mobile UI button instead of the previous
        // oversized themed card controls.
        var image = CreateUiObject(name, parent);
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(200f, 72f);
        image.color = new Color32(238, 238, 238, 255);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(250, 250, 250, 255);
        colors.pressedColor = new Color32(190, 190, 190, 255);
        colors.selectedColor = new Color32(225, 225, 225, 255);
        button.colors = colors;

        var text = CreateText(image.transform, label, 28f, FontStyles.Normal, new Color32(25, 25, 25, 255), TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, 12f, 12f, 8f, 8f);
        return image.gameObject;
    }

    static GameObject CreateInfoPanel(Transform parent)
    {
        var panel = CreateUiObject("Artwork Information Panel", parent);
        SetAnchors(panel.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.83f), Vector2.zero, Vector2.zero);
        panel.color = new Color(Navy.r, Navy.g, Navy.b, 0.965f);

        var background = new GameObject("Ocean Texture", typeof(RectTransform), typeof(RawImage));
        background.transform.SetParent(panel.transform, false);
        var raw = background.GetComponent<RawImage>();
        raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        raw.color = new Color(0.28f, 0.58f, 0.75f, 0.24f);
        raw.raycastTarget = false;
        SetStretch(background.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        var heading = CreateText(panel.transform, "THE KRAKEN'S EMBRACE", 50f, FontStyles.Bold, Gold, TextAlignmentOptions.TopLeft);
        heading.rectTransform.anchorMin = new Vector2(0.07f, 0.82f);
        heading.rectTransform.anchorMax = new Vector2(0.93f, 0.96f);
        heading.rectTransform.offsetMin = Vector2.zero;
        heading.rectTransform.offsetMax = Vector2.zero;

        var bodyText =
            "AR artwork \u201cThe Kraken's Embrace AR\u201d by Johern Lim (2026)\n\n" +
            "CONCEPT\nThe ship-free ocean poster becomes a living pirate scene. A 3D ship, moon, clouds, mist and lantern light exist only while the artwork is visible to the camera.\n\n" +
            "TRACKING + CONTROLS\nThe poster is both image marker and visual background. PIRATE SOUND toggles the theme and lower-volume ocean ambience together; INFO toggles this panel.\n\n" +
            "CREDITS / RIGHTS\n" +
            "2D artwork: \u201cShip-Free Ocean Poster / Ocean Tracking Marker\u201d, provided by Johern Lim as the source artwork and adapted for Unity image tracking. \u00a9 2026 Johern Lim; all rights reserved.\n\n" +
            "3D model: \u201cThe Krakens Embrace\u201d by mrkblckwd, Sketchfab, CC BY 4.0. Source: https://sketchfab.com/3d-models/the-krakens-embrace-dc51f373f4ee4f9bb0f035b0e1cf7b73\n\n" +
            "Ocean ambience: \u201cKraken Ocean Ambience\u201d by Johern Lim (2026), an original procedural 24-second loop generated for this project. Source: Tools/generate_ocean_audio.py (included). Licence: CC BY 4.0, https://creativecommons.org/licenses/by/4.0/\n\n" +
            "Music: \u201cMain Theme - Pirates of the Caribbean\u201d. Source supplied for this project: https://www.youtube.com/watch?v=rdB13lFexNk\n\n" +
            "Runtime: Unity AR Foundation and Unity Visual Scripting.";
        var body = CreateText(panel.transform, bodyText, 27f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
        body.enableAutoSizing = true;
        body.fontSizeMin = 19f;
        body.fontSizeMax = 27f;
        body.enableWordWrapping = true;
        body.rectTransform.anchorMin = new Vector2(0.07f, 0.06f);
        body.rectTransform.anchorMax = new Vector2(0.93f, 0.82f);
        body.rectTransform.offsetMin = Vector2.zero;
        body.rectTransform.offsetMax = Vector2.zero;
        return panel.gameObject;
    }

    static Image CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI CreateText(Transform parent, string content, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font != null)
            text.font = font;
        return text;
    }

    static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    static void SetStretch(RectTransform rect, float left, float right, float bottom, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static void AddMachine(GameObject target, ScriptGraphAsset graph)
    {
        var machine = target.AddComponent<ScriptMachine>();
        machine.nest.SwitchToMacro(graph);
    }

    [MenuItem("Tools/Kraken's Embrace/Build Android APK")]
    public static void BuildAndroidApk()
    {
        BuildCompleteAssignment();

        const string buildFolder = "Builds";
        const string outputPath = buildFolder + "/KrakensEmbraceAR.apk";
        Directory.CreateDirectory(buildFolder);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("Android build failed: " + report.summary.result);

        Debug.Log($"KRAKEN_ANDROID_BUILD_SUCCESS: {outputPath} ({report.summary.totalSize} bytes)");
    }

    static void ConfigurePlayerSettings()
    {
        PlayerSettings.productName = "The Kraken's Embrace AR";
        PlayerSettings.companyName = "UTAR Student Project";
        PlayerSettings.bundleVersion = "1.0.2";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.utar.krakensembracear");
        PlayerSettings.Android.bundleVersionCode = 3;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.iOS.cameraUsageDescription = "The camera is used to recognise The Kraken's Embrace poster and place the AR scene.";
        PlayerSettings.colorSpace = ColorSpace.Linear;
    }
}

[InitializeOnLoad]
public static class KrakensEmbraceBuildRequest
{
    const string RequestFile = "ProjectSettings/KrakensEmbraceBuildRequest.txt";

    static KrakensEmbraceBuildRequest()
    {
        EditorApplication.delayCall += RunWhenEditorIsReady;
    }

    static void RunWhenEditorIsReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunWhenEditorIsReady;
            return;
        }

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        var requestPath = projectRoot == null ? null : Path.Combine(projectRoot, RequestFile);
        if (requestPath == null || !File.Exists(requestPath))
            return;

        File.Delete(requestPath);
        KrakensEmbraceProjectBuilder.BuildCompleteAssignment();
    }
}

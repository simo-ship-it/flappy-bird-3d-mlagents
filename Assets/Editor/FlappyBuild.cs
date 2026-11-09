using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

public static class FlappyBuild
{
    private const string Sparrow = "Assets/Quirky Series Ultimate/FREE/Prefabs/Single LODs/Sparrow_LOD0.prefab";
    public static void BuildAll()
    {
        Prepare();
        BuildTrained();
    }
    [MenuItem("Flappy Lab/Prepare tutorial and training")]
    public static void Prepare()
    {
        ConfigureProject();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var pipe = PreparePipe();
        var world = BuildArena(pipe);
        Directory.CreateDirectory("Assets/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(world.gameObject, "Assets/Prefabs/CourseArena.prefab");
        UnityEngine.Object.DestroyImmediate(world.gameObject);
        CreateScene(prefab, false, true, null, "Lab3Tutorial");
        Build("Lab3Tutorial", "FlappyBird3D-Tutorial");
        CreateScene(prefab, true, false, null, "Training");
        Build("Training", "FlappyBird3D-Training");
    }
    [MenuItem("Flappy Lab/Build trained game")]
    public static void BuildPlayable()
    {
        ConfigureProject();
        var arena = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CourseArena.prefab");
        CreateScene(arena, false, true, null, "Lab3Tutorial");
        Build("Lab3Tutorial", "FlappyBird3D-Tutorial");
        BuildTrained();
    }

    [MenuItem("Flappy Lab/Build inference only")]
    public static void BuildTrained()
    {
        ConfigureProject();
        var arena = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CourseArena.prefab");
        var model = AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/Models/FlappyBird3D.onnx");
        if (model == null) throw new Exception("Trained ONNX model was not imported.");
        CreateScene(arena, false, false, model, "Inference");
        Build("Inference", "FlappyBird3D");
    }
    private static void ConfigureProject()
    {
        PlayerSettings.companyName = "NDVW";
        PlayerSettings.productName = "FlappyBird3D-MLAgents";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "edu.ndvw.flappybird3d");
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        GraphicsSettings.defaultRenderPipeline = null;
        QualitySettings.renderPipeline = null;
        Time.fixedDeltaTime = 0.02f;
        Physics.autoSyncTransforms = true;
        // Use the course materials with the built-in renderer, retaining their textures.
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] {"Assets"}))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
            Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
            // The supplied URP materials keep BaseColor serialized even when
            // their shader is unavailable in the built-in renderer.
            var serialized = new SerializedObject(material);
            var colors = serialized.FindProperty("m_SavedProperties.m_Colors");
            for (int i = 0; i < colors.arraySize; i++)
            {
                var pair = colors.GetArrayElementAtIndex(i);
                if (pair.FindPropertyRelative("first").stringValue == "_BaseColor")
                    color = pair.FindPropertyRelative("second").colorValue;
            }
            material.shader = Shader.Find("Standard");
            material.mainTexture = texture; material.color = color;
            EditorUtility.SetDirty(material);
        }
    }
    private static Material Material(string name, Color color)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = color; return mat;
    }
    private static GameObject PreparePipe()
    {
        var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Pipe.prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(original);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        var moves = instance.AddComponent<PipesMoves>();
        moves.top = instance.transform.Find("TopPipe");
        moves.bottom = instance.transform.Find("BottomPipe");
        moves.middle = instance.transform.Find("Middle Pipe");
        foreach (var body in new[] {moves.top, moves.bottom})
        {
            body.GetComponent<BoxCollider>().size = new Vector3(1, 2, 1);
            var cap = body.GetChild(0);
            foreach (var collider in cap.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            cap.gameObject.AddComponent<BoxCollider>().size = new Vector3(1, 2, 1);
        }
        var trigger = moves.middle.gameObject.AddComponent<PipeMiddleScript>(); trigger.pipe = moves;
        var prize = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        prize.name = "Extra life prize"; prize.transform.SetParent(instance.transform, false);
        prize.transform.localScale = Vector3.one * 1.6f;
        UnityEngine.Object.DestroyImmediate(prize.GetComponent<Collider>());
        prize.GetComponent<Renderer>().sharedMaterial = Material("ExtraLifeGold", new Color(1f, 0.68f, 0.12f));
        moves.prize = prize;
        moves.Configure(22, 12, false);
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Prefabs/CoursePipe.prefab");
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
    }
    private static FlappyWorld BuildArena(GameObject pipe)
    {
        var root = new GameObject("Course arena"); root.SetActive(false);
        var world = root.AddComponent<FlappyWorld>();
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor"; floor.transform.SetParent(root.transform, false); floor.transform.localScale = Vector3.one * 10;
        floor.GetComponent<Renderer>().sharedMaterial = Material("Floor", new Color(0.47f, 0.35f, 0.23f));
        var bird = new GameObject("Bird");
        bird.transform.SetParent(root.transform, false); bird.transform.localPosition = new Vector3(0, 12, 0);
        var character = bird.AddComponent<CharacterController>();
        character.height = 1.3f; character.radius = 0.55f; character.center = Vector3.zero;
        character.skinWidth = 0.03f; character.stepOffset = 0.1f; character.minMoveDistance = 0;
        var art = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Sparrow));
        art.transform.SetParent(bird.transform, false);
        art.transform.localScale = new Vector3(6, 3, 5);
        art.transform.localRotation = Quaternion.Euler(0, 90, 0);
        foreach (var c in art.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
        var bounds = art.GetComponentInChildren<Renderer>().bounds;
        art.transform.position += bird.transform.position - bounds.center;
        var motor = bird.AddComponent<BirdPlayerScript>(); motor.World = world;
        var behavior = bird.AddComponent<BehaviorParameters>();
        behavior.BehaviorName = "FlappyBird3D";
        behavior.BrainParameters.VectorObservationSize = 11;
        behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(2);
        var agent = bird.AddComponent<FlappyAgent>(); agent.World = world; agent.MaxStep = 4000;
        var requester = bird.AddComponent<DecisionRequester>(); requester.DecisionPeriod = 5; requester.TakeActionsBetweenDecisions = false;
        world.Agent = agent; world.Bird = motor;
        world.Pipes = new PipesMoves[3];
        for (int i = 0; i < 3; i++)
        {
            var p = ((GameObject)PrefabUtility.InstantiatePrefab(pipe)).GetComponent<PipesMoves>();
            p.transform.SetParent(root.transform, false); p.World = world; p.Configure(22 + i * 18, 12, i == 2);
            world.Pipes[i] = p;
        }
        return world;
    }
    private static void CreateScene(GameObject arena, bool training, bool tutorial, ModelAsset model, string name)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var controller = new GameObject("Game session").AddComponent<FlappySession>();
        controller.MainWorld = ((GameObject)PrefabUtility.InstantiatePrefab(arena)).GetComponent<FlappyWorld>();
        controller.Training = training; controller.Tutorial = tutorial; controller.Model = model;
        var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.55f, 0.76f, 0.88f);
        camera.fieldOfView = 48; camera.farClipPlane = 180; camera.nearClipPlane = 0.1f;
        camera.transform.position = new Vector3(8, 17, -42); camera.transform.LookAt(new Vector3(8, 12, 0));
        var follow = cameraObject.AddComponent<FollowPlayer>(); follow.player = controller.MainWorld.Bird.transform;
        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.15f; sun.transform.rotation = Quaternion.Euler(48, -35, 0);
        sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.8f);
        RenderSettings.fog = true; RenderSettings.fogColor = camera.backgroundColor;
        RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 65; RenderSettings.fogEndDistance = 150;
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/" + name + ".unity");
        EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene(scene.path, true)};
        AssetDatabase.SaveAssets();
    }
    private static void Build(string scene, string name)
    {
        string destination = Path.GetFullPath("Builds/" + name + ".app");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] {"Assets/Scenes/" + scene + ".unity"},
            target = BuildTarget.StandaloneOSX, locationPathName = destination, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
        Debug.Log("FLAPPY_BUILD_SUCCESS " + destination);
    }
}


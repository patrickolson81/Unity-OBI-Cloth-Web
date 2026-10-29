#nullable enable
namespace WebCloth.Validation
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using Obi;
    using UnityEditor;
    using UnityEditor.Rendering;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.LowLevel;
    using WebCloth.Editor;
    using WebCloth.Sample.Editor;

    public static class Verify
    {
        private const string Key = "WebCloth.Validation.Pending";
        private static bool finished;
        private static double deadline;
        private static InputSettings.EditorInputBehaviorInPlayMode originalInput;
        private static InputSettings.BackgroundBehavior originalBackground;
        private static readonly List<InputDevice> physical = new();

        public static void Run() => EditorApplication.delayCall += Begin;
        private static void Begin()
        {
            try
            {
                PrepareRenderPipeline();
                EditorSceneManager.OpenScene(ClothSample.ScenePath);
                ValidateBuildGuard();
                PlayerSettings.runInBackground = true;
                GameView.Capture();
                GameView.SetResolution(1060, 680);
                SessionState.SetBool(Key, true);
                EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Finish(error); }
        }
        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    finished = false;
                    originalInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                    originalBackground = InputSystem.settings.backgroundBehavior;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    foreach (var device in InputSystem.devices)
                        if (device.enabled) { physical.Add(device); InputSystem.DisableDevice(device, keepSendingEvents: true); }
                    deadline = EditorApplication.timeSinceStartup + 120;
                    EditorApplication.update += Timeout;
                    new GameObject("Cloth validation").AddComponent<VerificationHost>().StartCoroutine(Guard(Observe()));
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
                }
            };
        }
        private static void Timeout() { if (EditorApplication.timeSinceStartup > deadline) Finish(new TimeoutException("Cloth validation timed out.")); }
        private static void Finish(Exception? error = null)
        {
            if (finished) return;
            finished = true;
            EditorApplication.update -= Timeout;
            foreach (var device in physical) if (device.added) InputSystem.EnableDevice(device);
            physical.Clear();
            InputSystem.settings.editorInputBehaviorInPlayMode = originalInput;
            InputSystem.settings.backgroundBehavior = originalBackground;
            Directory.CreateDirectory("Reports");
            File.WriteAllText("Reports/play.txt", error == null ? "PASS: independent sample; CPU scene/runtime-prefab build guard and rejected-build setting preservation; 81 finite particles and two fixed pins before and after interaction; visible mouse/touch movement, off-center edge grabs, final release and resize cancellation at phone, tablet, square and ultrawide sizes." : "FAIL: " + error);
            SessionState.SetInt(Key + ".exit", error == null ? 0 : 1);
            if (EditorApplication.isPlaying) { GameView.Restore(); EditorApplication.isPlaying = false; }
            else { SessionState.SetBool(Key, false); EditorApplication.Exit(error == null ? 0 : 1); }
        }
        private static IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                object? next;
                try
                {
                    var active = stack.Peek();
                    if (!active.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    next = active.Current;
                }
                catch (Exception error) { Finish(error); yield break; }
                if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
            Finish();
        }
        private static IEnumerator Observe()
        {
            var cloth = UnityEngine.Object.FindFirstObjectByType<ObiCloth>();
            yield return new WaitForSeconds(3);
            if (!cloth.isLoaded || cloth.activeParticleCount != 81 || cloth.solver.backendType != ObiSolver.BackendType.Burst)
                throw new InvalidOperationException("The CPU cloth did not load 81 particles.");
            ValidateClothState(cloth, requireDrape: true);
            yield return VerifyDragging(cloth);
            ValidateClothState(cloth, requireDrape: false);
        }
        private static void ValidateClothState(ObiCloth cloth, bool requireDrape)
        {
            if (!cloth.isLoaded || cloth.activeParticleCount != 81 || cloth.solver.backendType != ObiSolver.BackendType.Burst)
                throw new InvalidOperationException("The CPU cloth lost its loaded 81-particle state during interaction.");
            var pinned = 0;
            var moved = 0;
            for (var i = 0; i < cloth.activeParticleCount; i++)
            {
                var actual = (Vector3)cloth.solver.positions[cloth.solverIndices[i]];
                var initial = cloth.solver.transform.InverseTransformPoint(cloth.transform.TransformPoint(cloth.clothBlueprint.positions[i]));
                if (!float.IsFinite(actual.x) || !float.IsFinite(actual.y) || !float.IsFinite(actual.z)) throw new InvalidOperationException("Non-finite cloth particle.");
                if (cloth.clothBlueprint.invMasses[i] == 0)
                {
                    pinned++;
                    if (Vector3.Distance(actual, initial) > 0.01f) throw new InvalidOperationException("Fixed corner moved.");
                }
                else if (initial.y - actual.y > 0.1f) moved++;
            }
            if (pinned != 2) throw new InvalidOperationException("Cloth lost a fixed corner.");
            if (requireDrape && moved < 20) throw new InvalidOperationException("Cloth did not drape.");
        }
        private static void PrepareRenderPipeline()
        {
            const string path = "Assets/Cloth Validation Pipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/Cloth Validation Renderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                var settings = new SerializedObject(pipeline);
                settings.FindProperty("m_MainLightShadowsSupported").boolValue = false;
                settings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
                settings.FindProperty("m_AnyShadowsSupported").boolValue = false;
                settings.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.Disabled;
                settings.ApplyModifiedPropertiesWithoutUndo();
                pipeline.supportsCameraDepthTexture = false;
                pipeline.supportsCameraOpaqueTexture = false;
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 1;
                AssetDatabase.CreateAsset(pipeline, path);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();
        }
        private static void ValidateBuildGuard()
        {
            var options = new BuildPlayerOptions { scenes = new[] { ClothSample.ScenePath }, target = BuildTarget.WebGL };
            CpuWebClothBuild.Validate(options);
            options.target = BuildTarget.StandaloneOSX;
            try { CpuWebClothBuild.Validate(options); throw new Exception("Non-web build was accepted."); }
            catch (ArgumentException) { }
            options.target = BuildTarget.WebGL;
            var gpu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var actor = new GameObject("GPU solver").AddComponent<ObiSolver>();
                actor.backendType = ObiSolver.BackendType.Compute;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor.gameObject, gpu);
                EditorSceneManager.SaveScene(gpu, "Assets/Rejected GPU.unity");
                try { CpuWebClothBuild.Validate(new BuildPlayerOptions { scenes = new[] { "Assets/Rejected GPU.unity" }, target = BuildTarget.WebGL }); throw new Exception("Compute solver was accepted."); }
                catch (InvalidOperationException) { }
            }
            finally { EditorSceneManager.CloseScene(gpu, true); AssetDatabase.DeleteAsset("Assets/Rejected GPU.unity"); }
            ValidateRuntimePrefabs(options);
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Obi/Resources/Compute/FluidFoamCollisions.compute");
            if (shader == null) throw new InvalidOperationException("Expected Obi compute shader not found for guard test.");
            var data = new List<ShaderCompilerData> { default };
            var stripper = new CpuWebClothShaderStripper();
            stripper.OnProcessComputeShader(shader, "", data);
            if (data.Count != 1) throw new InvalidOperationException("Unscoped build stripped Obi shaders.");
            const string key = "WebCloth.CpuBuild.ComputeFolder";
            try
            {
                SessionState.SetString(key, "Assets/Obi/Resources/Compute/");
                stripper.OnProcessComputeShader(shader, "", data);
                if (data.Count != 0) throw new InvalidOperationException("CPU build did not strip Obi compute variants.");
                data.Add(default);
                stripper.OnProcessComputeShader(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Other.compute"), "", data);
                if (data.Count != 1) throw new InvalidOperationException("CPU build stripped another compute shader.");
            }
            finally { SessionState.EraseString(key); }
        }

        private static void ValidateRuntimePrefabs(BuildPlayerOptions sampleOptions)
        {
            const string folder = "Assets/Runtime Solver Validation";
            const string scopeKey = "WebCloth.CpuBuild.ComputeFolder";
            AssetDatabase.CreateFolder("Assets", "Runtime Solver Validation");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject("Runtime CPU cloth");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var solverObject = new GameObject("Inactive nested CPU solver");
            solverObject.transform.SetParent(root.transform);
            solverObject.AddComponent<ObiSolver>().backendType = ObiSolver.BackendType.Burst;
            solverObject.SetActive(false);
            var cpu = PrefabUtility.SaveAsPrefabAsset(root, folder + "/CPU.prefab");
            var gpuObject = new GameObject("Inactive nested Compute solver");
            gpuObject.transform.SetParent(solverObject.transform);
            gpuObject.AddComponent<ObiSolver>().backendType = ObiSolver.BackendType.Compute;
            var gpu = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Compute.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            root = new GameObject("Empty prefab");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var empty = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Empty.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.SaveScene(scene, folder + "/Runtime.unity");
            var options = new BuildPlayerOptions { scenes = new[] { folder + "/Runtime.unity" }, target = BuildTarget.WebGL };
            var previousScope = SessionState.GetString(scopeKey, string.Empty);
            var automatic = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL);
            var graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
            GameObject? instance = null;
            try
            {
                CpuWebClothBuild.Validate(options, new[] { cpu });
                RequireRejected<InvalidOperationException>(() => CpuWebClothBuild.Validate(options), "Runtime-only scene without declared solvers");
                RequireRejected<InvalidOperationException>(() => CpuWebClothBuild.Validate(options, Array.Empty<GameObject>()), "Empty runtime declarations");
                RequireRejected<InvalidOperationException>(() => CpuWebClothBuild.Validate(sampleOptions, new[] { gpu }), "Inactive nested Compute prefab solver");
                RequireRejected<InvalidOperationException>(() => CpuWebClothBuild.Validate(sampleOptions, new[] { empty }), "Solver-free prefab declaration");
                RequireRejected<ArgumentNullException>(() => CpuWebClothBuild.Validate(options, null!), "Null declaration list");
                RequireRejected<ArgumentException>(() => CpuWebClothBuild.Validate(options, new GameObject[] { null! }), "Null declaration");
                RequireRejected<ArgumentException>(() => CpuWebClothBuild.Validate(options, new[] { cpu.transform.GetChild(0).gameObject }), "Prefab child declaration");
                instance = (GameObject)PrefabUtility.InstantiatePrefab(cpu, scene);
                RequireRejected<ArgumentException>(() => CpuWebClothBuild.Validate(options, new[] { instance! }), "Scene prefab instance declaration");
                SessionState.SetString(scopeKey, "Existing validation scope");
                RequireRejected<InvalidOperationException>(() => CpuWebClothBuild.Build(options, new[] { gpu }), "Rejected runtime build");
                if (SessionState.GetString(scopeKey, string.Empty) != "Existing validation scope" || PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL) != automatic)
                    throw new InvalidOperationException("Rejected runtime build changed the existing build scope or graphics selection.");
                var afterGraphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
                if (afterGraphics.Length != graphics.Length) throw new InvalidOperationException("Rejected runtime build changed graphics APIs.");
                for (var i = 0; i < graphics.Length; i++)
                    if (afterGraphics[i] != graphics[i]) throw new InvalidOperationException("Rejected runtime build changed graphics APIs.");
            }
            finally
            {
                SessionState.SetString(scopeKey, previousScope);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static void RequireRejected<T>(Action action, string description) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(description + " was accepted.");
        }
        private static IEnumerator VerifyDragging(ObiCloth cloth)
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var touch = InputSystem.AddDevice<Touchscreen>();
            var drag = UnityEngine.Object.FindFirstObjectByType<SphereDrag>();
            var camera = Camera.main;
            foreach (var size in new[] { new Vector2Int(360, 640), new Vector2Int(768, 1024), new Vector2Int(800, 800), new Vector2Int(2560, 1080) })
            {
                GameView.SetResolution(size.x, size.y);
                yield return null; yield return null;
                yield return WaitForSettled(drag);
                foreach (var touching in new[] { false, true })
                {
                    var point = (Vector2)camera.WorldToScreenPoint(drag.transform.position);
                    var before = drag.transform.position;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                    yield return null; yield return null;
                    if (touching) InputSystem.QueueStateEvent(touch, new TouchState { touchId = 7, position = point, phase = UnityEngine.InputSystem.TouchPhase.Began });
                    else InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                    yield return null; yield return null;
                    if (!drag.IsDragging) throw new InvalidOperationException("Sphere could not be grabbed with " + (touching ? "touch" : "mouse") + " at " + size);
                    var destination = point + Vector2.right * (point.x > size.x * 0.5f ? -1 : 1) * Mathf.Min(60, size.x * 0.15f);
                    if (touching) InputSystem.QueueStateEvent(touch, new TouchState { touchId = 7, position = destination, phase = UnityEngine.InputSystem.TouchPhase.Moved });
                    else InputSystem.QueueStateEvent(mouse, new MouseState { position = destination }.WithButton(MouseButton.Left));
                    yield return new WaitForSeconds(0.5f);
                    if (Vector3.Distance(before, drag.transform.position) < 0.05f) throw new InvalidOperationException("Held pointer did not move the collision sphere.");
                    var releasePoint = destination + Vector2.up * 12;
                    if (touching) InputSystem.QueueStateEvent(touch, new TouchState { touchId = 7, position = releasePoint, phase = UnityEngine.InputSystem.TouchPhase.Ended });
                    else InputSystem.QueueStateEvent(mouse, new MouseState { position = releasePoint });
                    yield return null; yield return null;
                    if (drag.IsDragging) throw new InvalidOperationException("Released sphere remained grabbed.");
                    yield return new WaitForSeconds(0.5f);
                    if (Vector2.Distance((Vector2)camera.WorldToScreenPoint(drag.transform.position), releasePoint) > 3)
                        throw new InvalidOperationException("The final release position was lost at " + size);
                }
                yield return new WaitForEndOfFrame();
                var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                Directory.CreateDirectory("Reports");
                File.WriteAllBytes($"Reports/{size.x}x{size.y}.png", screenshot.EncodeToPNG());
                var orange = 0;
                var cyan = 0;
                foreach (var pixel in screenshot.GetPixels32())
                {
                    if (pixel.r > 80 && pixel.r > pixel.g * 1.5f && pixel.g > pixel.b * 1.3f) orange++;
                    if (pixel.g > 70 && pixel.g > pixel.r * 1.5f && pixel.b > pixel.r * 1.5f) cyan++;
                }
                UnityEngine.Object.Destroy(screenshot);
                if (orange < 100 || cyan < 100) throw new InvalidOperationException("The sphere or cloth was not visible at " + size);
                if (size.x == 800 && size.y == 800)
                {
                    yield return VerifyEdgeGrab(mouse, touch, drag, camera, touching: false, rightEdge: true);
                    yield return VerifyEdgeGrab(mouse, touch, drag, camera, touching: true, rightEdge: false);
                    yield return MoveSphereToCenter(mouse, touch, drag, camera);
                }
                ValidateClothState(cloth, requireDrape: false);
                var center = (Vector2)camera.WorldToScreenPoint(drag.transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                GameView.SetResolution(size.x == size.y ? size.x + 1 : size.y, size.x);
                yield return null; yield return null; yield return null;
                if (drag.IsDragging) throw new InvalidOperationException("Resizing retained a stale grab at " + size);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                yield return null; yield return null;
                yield return WaitForSettled(drag);
                AssertSphereWithinViewport(drag, camera);
                ValidateClothState(cloth, requireDrape: false);
            }
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(touch);
        }

        private static IEnumerator VerifyEdgeGrab(Mouse mouse, Touchscreen touch, SphereDrag drag, Camera camera, bool touching, bool rightEdge)
        {
            yield return WaitForSettled(drag);
            var before = drag.transform.position;
            var center = (Vector2)camera.WorldToScreenPoint(before);
            var radius = Mathf.Abs(camera.WorldToScreenPoint(before + camera.transform.right * drag.Radius).x - center.x);
            var point = center + Vector2.right * radius * (rightEdge ? -0.65f : 0.65f);
            var grabs = drag.GrabCount;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null; yield return null;
            QueuePointer(mouse, touch, point, touching, pressed: true, began: true);
            yield return null; yield return null;
            if (!drag.IsDragging || drag.GrabCount != grabs + 1)
                throw new InvalidOperationException("An off-center sphere grab was not captured.");
            if (Vector3.Distance(before, drag.transform.position) > 0.02f)
                throw new InvalidOperationException("An off-center grab made the sphere jump.");
            var destination = new Vector2(rightEdge ? camera.pixelRect.xMax - 2 : camera.pixelRect.xMin + 2, point.y);
            QueuePointer(mouse, touch, destination, touching, pressed: true, began: false);
            yield return null; yield return null;
            yield return WaitForSettled(drag);
            if (!drag.IsDragging || Vector3.Distance(before, drag.transform.position) < 0.05f)
                throw new InvalidOperationException("The off-center edge grab did not move the sphere.");
            AssertSphereWithinViewport(drag, camera);
            var finalCenter = (Vector2)camera.WorldToScreenPoint(drag.transform.position);
            var finalRadius = Mathf.Abs(camera.WorldToScreenPoint(drag.transform.position + camera.transform.right * drag.Radius).x - finalCenter.x);
            var edgeGap = rightEdge ? camera.pixelRect.xMax - finalCenter.x - finalRadius : finalCenter.x - finalRadius - camera.pixelRect.xMin;
            if (edgeGap > 8) throw new InvalidOperationException($"The off-center drag did not reach the viewport edge: touch={touching}, right={rightEdge}, gap={edgeGap}, center={finalCenter}, radius={finalRadius}, pointer={destination}, moving={drag.IsMoving}, viewport={camera.pixelRect}.");
            QueuePointer(mouse, touch, destination, touching, pressed: false, began: false);
            yield return null; yield return null;
            if (drag.IsDragging) throw new InvalidOperationException("An off-center edge grab remained captured after release.");
            yield return WaitForSettled(drag);
            AssertSphereWithinViewport(drag, camera);
        }
        private static IEnumerator MoveSphereToCenter(Mouse mouse, Touchscreen touch, SphereDrag drag, Camera camera)
        {
            var point = (Vector2)camera.WorldToScreenPoint(drag.transform.position);
            QueuePointer(mouse, touch, point, touching: false, pressed: true, began: true);
            yield return null; yield return null;
            if (!drag.IsDragging) throw new InvalidOperationException("Could not grab the sphere to return it to center.");
            var destination = new Vector2(camera.pixelRect.center.x, point.y);
            QueuePointer(mouse, touch, destination, touching: false, pressed: true, began: false);
            yield return null; yield return null;
            yield return WaitForSettled(drag);
            QueuePointer(mouse, touch, destination, touching: false, pressed: false, began: false);
            yield return null; yield return null;
            yield return WaitForSettled(drag);
        }
        private static void QueuePointer(Mouse mouse, Touchscreen touch, Vector2 point, bool touching, bool pressed, bool began)
        {
            if (touching)
            {
                var phase = !pressed ? UnityEngine.InputSystem.TouchPhase.Ended : began ? UnityEngine.InputSystem.TouchPhase.Began : UnityEngine.InputSystem.TouchPhase.Moved;
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 11, position = point, phase = phase });
            }
            else InputSystem.QueueStateEvent(mouse, pressed ? new MouseState { position = point }.WithButton(MouseButton.Left) : new MouseState { position = point });
        }
        private static IEnumerator WaitForSettled(SphereDrag drag)
        {
            var until = Time.realtimeSinceStartup + 8;
            while (drag.IsMoving)
            {
                if (Time.realtimeSinceStartup > until) throw new TimeoutException("The dragged sphere did not settle.");
                yield return null;
            }
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
        }
        private static void AssertSphereWithinViewport(SphereDrag drag, Camera camera)
        {
            var center = camera.WorldToScreenPoint(drag.transform.position);
            var radius = Mathf.Abs(camera.WorldToScreenPoint(drag.transform.position + camera.transform.right * drag.Radius).x - center.x);
            var bounds = camera.pixelRect;
            if (center.x - radius < bounds.xMin - 1 || center.x + radius > bounds.xMax + 1 || center.y - radius < bounds.yMin - 1 || center.y + radius > bounds.yMax + 1)
                throw new InvalidOperationException("The grabbed sphere was clipped by the viewport.");
        }

    }
}

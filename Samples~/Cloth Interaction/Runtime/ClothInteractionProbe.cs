#nullable enable
namespace WebCloth.Sample
{
    using System;
    using System.Runtime.InteropServices;
    using Obi;
    using UnityEngine;

    public sealed class ClothInteractionProbe : MonoBehaviour
    {
        [SerializeField] private ObiCloth cloth = null!;
        [SerializeField] private SphereDrag ball = null!;
        private readonly float[] frameTimes = new float[120];
        private readonly float[] sortedFrames = new float[120];
        private readonly Result result = new();
        private Vector3[] previousPositions = Array.Empty<Vector3>();
        private int frameIndex;
        private int frameCount;
        private double idleTime;
        private double dragTime;
        private float started;
        private float publishAt;
        private float sampleAfter;
        private bool failed;

        [Serializable]
        private sealed class Result
        {
            public bool done;
            public bool success;
            public string message = string.Empty;
            public string backend = string.Empty;
            public string graphics = string.Empty;
            public int particles;
            public int pinned;
            public int moved;
            public float simulationSeconds;
            public float averageFrameMs;
            public float p95FrameMs;
            public float worstFrameMs;
            public float idleAverageMs;
            public float dragAverageMs;
            public int idleFrames;
            public int dragFrames;
            public bool dragging;
            public bool moving;
            public int grabs;
            public float ballTravel;
            public float ballViewportX;
            public float ballViewportY;
            public float clothMotion;
            public float dragClothMotionMax;
        }

        public void Init(ObiCloth cloth, SphereDrag ball) { this.cloth = cloth; this.ball = ball; }

        private void Start()
        {
            started = Time.realtimeSinceStartup;
            result.message = "Settling cloth… grab the orange ball and drag";
            result.graphics = SystemInfo.graphicsDeviceType.ToString();
            Publish(result);
        }

        private void Update()
        {
            if (failed) return;
            var now = Time.realtimeSinceStartup;
            if (result.done && now > sampleAfter && (Application.isFocused || Application.isEditor))
            {
                var ms = Time.unscaledDeltaTime * 1000;
                frameTimes[frameIndex] = ms;
                frameIndex = (frameIndex + 1) % frameTimes.Length;
                frameCount = Mathf.Min(frameCount + 1, frameTimes.Length);
                if (ball.IsDragging || ball.IsMoving) { result.dragFrames++; dragTime += ms; }
                else { result.idleFrames++; idleTime += ms; }
            }
            if (now < publishAt) return;
            publishAt = now + 0.5f;
            if (now - started > 30 && (!cloth.isLoaded || cloth.solver.timeSinceSimulationStart < 3))
            {
                Fail("Cloth did not simulate three seconds within the 30-second startup deadline");
                return;
            }
            if (!cloth.isLoaded) return;
            var solver = cloth.solver;
            result.done = solver.timeSinceSimulationStart >= 3;
            result.success = solver.backendType == ObiSolver.BackendType.Burst && cloth.activeParticleCount == 81;
            result.backend = solver.implementation.GetType().Name;
            result.particles = cloth.activeParticleCount;
            result.simulationSeconds = solver.timeSinceSimulationStart;
            result.dragging = ball.IsDragging;
            result.moving = ball.IsMoving;
            result.grabs = ball.GrabCount;
            result.ballTravel = ball.TravelDistance;
            var viewport = ball.ViewportPosition;
            result.ballViewportX = viewport.x;
            result.ballViewportY = viewport.y;
            result.pinned = result.moved = 0;
            result.clothMotion = 0;
            if (previousPositions.Length != cloth.activeParticleCount) previousPositions = new Vector3[cloth.activeParticleCount];
            for (var i = 0; i < cloth.activeParticleCount; i++)
            {
                var actual = (Vector3)solver.positions[cloth.solverIndices[i]];
                var initial = solver.transform.InverseTransformPoint(cloth.transform.TransformPoint(cloth.clothBlueprint.positions[i]));
                result.success &= float.IsFinite(actual.x) && float.IsFinite(actual.y) && float.IsFinite(actual.z);
                if (cloth.clothBlueprint.invMasses[i] == 0)
                {
                    result.pinned++;
                    result.success &= Vector3.Distance(actual, initial) < 0.01f;
                }
                else
                {
                    if (initial.y - actual.y > 0.1f) result.moved++;
                    if (result.done) result.clothMotion = Mathf.Max(result.clothMotion, Vector3.Distance(actual, previousPositions[i]));
                }
                previousPositions[i] = actual;
            }
            result.success &= result.pinned == 2;
            if (ball.IsDragging || ball.IsMoving) result.dragClothMotionMax = Mathf.Max(result.dragClothMotionMax, result.clothMotion);
            if (frameCount > 0)
            {
                Array.Copy(frameTimes, sortedFrames, frameCount);
                Array.Sort(sortedFrames, 0, frameCount);
                var total = 0f;
                for (var i = 0; i < frameCount; i++) total += sortedFrames[i];
                result.averageFrameMs = total / frameCount;
                result.p95FrameMs = sortedFrames[Mathf.CeilToInt(frameCount * 0.95f) - 1];
                result.worstFrameMs = sortedFrames[frameCount - 1];
            }
            result.idleAverageMs = result.idleFrames > 0 ? (float)(idleTime / result.idleFrames) : 0;
            result.dragAverageMs = result.dragFrames > 0 ? (float)(dragTime / result.dragFrames) : 0;
            result.message = "Grab the orange ball and drag · mouse or touch";
            Publish(result);
            if (!result.success) Fail("Cloth particles or fixed corners became invalid during interaction");
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) return;
            frameCount = frameIndex = 0;
            sampleAfter = Time.realtimeSinceStartup + 0.5f;
        }

        private void Fail(string message)
        {
            failed = true;
            result.done = true;
            result.success = false;
            result.message = message;
            Publish(result);
            throw new InvalidOperationException(message);
        }

        private static void Publish(Result result)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebClothReport(JsonUtility.ToJson(result));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WebClothReport(string json);
#endif
    }
}

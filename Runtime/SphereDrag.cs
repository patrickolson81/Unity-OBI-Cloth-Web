#nullable enable
namespace WebCloth
{
    using System;
    using UnityEngine;

    public sealed class SphereDrag : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera = null!;
        [SerializeField] private Rigidbody body = null!;
        [SerializeField] private SphereCollider sphere = null!;
        [SerializeField, Min(0.1f)] private float maximumSpeed = 3;
        [SerializeField] private float floorHeight;
        private DragPointerInput? input;
        private Plane dragPlane;
        private Vector3 grabOffset;
        private Vector3 target;
        private Rect viewport;
        public bool IsDragging { get; private set; }
        public int GrabCount { get; private set; }
        public float TravelDistance { get; private set; }
        public bool IsMoving => body != null && (target - body.position).sqrMagnitude > 0.00001f;
        public float Radius => sphere.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Max(Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z)));
        public Vector3 ViewportPosition => viewCamera.WorldToViewportPoint(transform.position);

        public void Init(Camera camera, Rigidbody rigidbody, SphereCollider collider)
        {
            viewCamera = camera;
            body = rigidbody;
            sphere = collider;
        }

        private void Start()
        {
            if (viewCamera == null || body == null || sphere == null) throw new InvalidOperationException("Assign the drag camera, kinematic rigidbody and sphere collider before playing.");
            target = body.position;
            viewport = viewCamera.pixelRect;
            input = new DragPointerInput(Blocks, Cancel);
        }

        private bool Blocks(Vector2 point) => !viewCamera.pixelRect.Contains(point) || !sphere.Raycast(viewCamera.ScreenPointToRay(point), out _, 100);

        private void Update()
        {
            if (input == null) return;
            if (viewport != viewCamera.pixelRect)
            {
                viewport = viewCamera.pixelRect;
                Cancel();
                target = ClampToViewport(body.position);
            }
            var phase = input.Read(out var point);
            if (phase == DragPointerPhase.Pressed)
            {
                dragPlane = new Plane(viewCamera.transform.forward, body.position);
                var ray = viewCamera.ScreenPointToRay(point);
                if (!dragPlane.Raycast(ray, out var distance)) { Cancel(); return; }
                grabOffset = body.position - ray.GetPoint(distance);
                IsDragging = true;
                GrabCount++;
            }
            var released = phase == DragPointerPhase.Released && IsDragging;
            if (released) IsDragging = false;
            if (!IsDragging && !released) return;
            var pointerRay = viewCamera.ScreenPointToRay(point);
            if (!dragPlane.Raycast(pointerRay, out var depth)) return;
            target = ClampToViewport(pointerRay.GetPoint(depth) + grabOffset);
        }

        private Vector3 ClampToViewport(Vector3 position)
        {
            position.y = Mathf.Max(floorHeight + Radius + 0.02f, position.y);
            var center = viewCamera.WorldToScreenPoint(position);
            var edge = viewCamera.WorldToScreenPoint(position + viewCamera.transform.right * Radius);
            var margin = Mathf.Min(Mathf.Abs(edge.x - center.x) + 4, Mathf.Min(viewport.width, viewport.height) * 0.45f);
            center.x = Mathf.Clamp(center.x, viewport.xMin + margin, viewport.xMax - margin);
            center.y = Mathf.Clamp(center.y, viewport.yMin + margin, viewport.yMax - margin);
            position = viewCamera.ScreenToWorldPoint(center);
            position.y = Mathf.Max(floorHeight + Radius + 0.02f, position.y);
            return position;
        }

        private void FixedUpdate()
        {
            if (input == null || !IsMoving) return;
            var next = Vector3.MoveTowards(body.position, target, maximumSpeed * Time.fixedDeltaTime);
            TravelDistance += Vector3.Distance(body.position, next);
            body.MovePosition(next);
        }

        private void Cancel()
        {
            IsDragging = false;
            if (body != null) target = body.position;
            input?.Reset();
        }

        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        private void OnDisable() => Cancel();
    }
}

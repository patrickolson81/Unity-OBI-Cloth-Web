#nullable enable
namespace WebCloth
{
    using System;
    using UnityEngine;
    using UnityEngine.InputSystem;

    internal enum DragPointerPhase { None, Pressed, Held, Released }
    internal sealed class DragPointerInput
    {
        private readonly Func<Vector2, bool> blocks;
        private readonly Action canceled;
        private int touchId;
        private bool mouseCaptured;
        private bool requireRelease = true;
        private Vector2 lastPoint;
        public DragPointerInput(Func<Vector2, bool> blocks, Action canceled) { this.blocks = blocks; this.canceled = canceled; }
        private static bool PointerHeld()
        {
            if (Mouse.current?.leftButton.isPressed == true) return true;
            if (Touchscreen.current is not { } screen) return false;
            foreach (var touch in screen.touches) if (touch.press.isPressed) return true;
            return false;
        }
        public void Reset() { touchId = 0; mouseCaptured = false; requireRelease = PointerHeld(); }
        public DragPointerPhase Read(out Vector2 point)
        {
            point = lastPoint;
            if (requireRelease)
            {
                if (!PointerHeld()) requireRelease = false;
                return DragPointerPhase.None;
            }
            if (Touchscreen.current is { } screen)
            {
                if (touchId != 0)
                {
                    foreach (var touch in screen.touches)
                    {
                        if (touch.touchId.ReadValue() != touchId) continue;
                        point = lastPoint = touch.position.ReadValue();
                        if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                        {
                            Reset(); canceled(); return DragPointerPhase.None;
                        }
                        if (!touch.press.isPressed) { Reset(); return DragPointerPhase.Released; }
                        return DragPointerPhase.Held;
                    }
                    Reset(); canceled(); return DragPointerPhase.None;
                }
                var anyTouch = false;
                foreach (var touch in screen.touches)
                {
                    anyTouch |= touch.press.isPressed;
                    if (mouseCaptured || !touch.press.wasPressedThisFrame || blocks(touch.position.ReadValue())) continue;
                    touchId = touch.touchId.ReadValue();
                    point = lastPoint = touch.position.ReadValue();
                    return DragPointerPhase.Pressed;
                }
                if (anyTouch && !mouseCaptured) return DragPointerPhase.None;
            }
            var mouse = Mouse.current;
            if (mouse == null) return DragPointerPhase.None;
            point = lastPoint = mouse.position.ReadValue();
            if (mouseCaptured)
            {
                if (!mouse.leftButton.isPressed) { Reset(); return DragPointerPhase.Released; }
                return DragPointerPhase.Held;
            }
            if (!mouse.leftButton.wasPressedThisFrame || blocks(point)) return DragPointerPhase.None;
            mouseCaptured = true;
            return DragPointerPhase.Pressed;
        }
    }
}

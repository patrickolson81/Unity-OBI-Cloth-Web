#nullable enable
namespace WebCloth.Validation
{
    using System;
    using System.Reflection;
    using UnityEditor;

    internal static class GameView
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        [Serializable]
        private sealed class Snapshot
        {
            public int selected;
            public int customCount;
            public string behavior = string.Empty;
        }
        private static readonly Type ViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")!;
        internal static EditorWindow PrepareUnfocused()
        {
            var view = EditorWindow.GetWindow(ViewType, false, "Game", false);
            var property = ViewType.GetProperty("enterPlayModeBehavior", Instance)!;
            property.SetValue(view, Enum.Parse(property.PropertyType, "PlayUnfocused"));
            if (property.GetValue(view)!.ToString() != "PlayUnfocused") throw new InvalidOperationException("Game view must play unfocused.");
            return view;
        }
        internal static void Capture()
        {
            var view = EditorWindow.GetWindow(ViewType, false, "Game", false);
            var sizes = Sizes();
            var state = new Snapshot
            {
                behavior = ViewType.GetProperty("enterPlayModeBehavior", Instance)!.GetValue(view)!.ToString(),
                selected = (int)ViewType.GetProperty("selectedSizeIndex", Instance)!.GetValue(view),
                customCount = (int)sizes.GetType().GetMethod("GetCustomCount")!.Invoke(sizes, null)
            };
            SessionState.SetString("WebCloth.Validation.GameView", UnityEngine.JsonUtility.ToJson(state));
        }
        internal static void SetResolution(int width, int height)
        {
            var assembly = typeof(EditorWindow).Assembly;
            var sizes = Sizes();
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize")!, Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType")!, "FixedResolution"), width, height, "Cloth verification");
            var type = sizes.GetType();
            var count = (int)type.GetMethod("GetBuiltinCount")!.Invoke(sizes, null) + (int)type.GetMethod("GetCustomCount")!.Invoke(sizes, null);
            type.GetMethod("AddCustomSize")!.Invoke(sizes, new[] { size });
            var view = PrepareUnfocused();
            ViewType.GetProperty("selectedSizeIndex", Instance)!.SetValue(view, count);
            view.Repaint();
        }
        internal static void Restore()
        {
            var view = EditorWindow.GetWindow(ViewType, false, "Game", false);
            var state = UnityEngine.JsonUtility.FromJson<Snapshot>(SessionState.GetString("WebCloth.Validation.GameView", "{}"));
            ViewType.GetProperty("selectedSizeIndex", Instance)!.SetValue(view, state.selected);
            var behavior = ViewType.GetProperty("enterPlayModeBehavior", Instance)!;
            if (state.behavior.Length > 0) behavior.SetValue(view, Enum.Parse(behavior.PropertyType, state.behavior));
            var sizes = Sizes();
            var type = sizes.GetType();
            var count = (int)type.GetMethod("GetCustomCount")!.Invoke(sizes, null);
            var builtin = (int)type.GetMethod("GetBuiltinCount")!.Invoke(sizes, null);
            for (var i = count - 1; i >= state.customCount; i--) type.GetMethod("RemoveCustomSize")!.Invoke(sizes, new object[] { builtin + i });
        }
        private static object Sizes()
        {
            var assembly = typeof(EditorWindow).Assembly;
            var type = assembly.GetType("UnityEditor.GameViewSizes")!;
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
            return type.GetMethod("GetGroup")!.Invoke(sizes, new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType")!, "Standalone") })!;
        }
    }
}

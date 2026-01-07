#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

// NOTE:
// This editor utility intentionally avoids compile-time references to runtime scripts
// (Proto_PhaseDirector / Proto_PhaseHUD / Proto_UI) to prevent CS0246 when namespaces
// or assembly definitions differ. It resolves types via reflection by name.
public static class ProtoOneShotSetup
{
    private const string RootName = "__PROTO_SYSTEMS__";

    [MenuItem("Tools/OJika Proto/One Shot Setup (Phase System)")]
    public static void Run()
    {
        var root = GameObject.Find(RootName);
        if (root == null) root = new GameObject(RootName);

        EnsureEventSystem();

        // Resolve types (supports namespaces)
        var tPhaseDirector = FindTypeByName("Proto_PhaseDirector");
        var tPhaseHud      = FindTypeByName("Proto_PhaseHUD");
        var tProtoUI       = FindTypeByName("Proto_UI");

        if (tPhaseDirector == null || tPhaseHud == null)
        {
            EditorUtility.DisplayDialog(
                "One Shot Setup",
                "必要な型が見つかりませんでした。\n\n" +
                $"Proto_PhaseDirector: {(tPhaseDirector != null ? "OK" : "NG")}\n" +
                $"Proto_PhaseHUD: {(tPhaseHud != null ? "OK" : "NG")}\n\n" +
                "対処: Assets/Scripts に該当スクリプトが存在し、コンパイルエラーが無いか確認してください。\n" +
                "（asmdef/namespaceでも動くように反射で探しています）",
                "OK");
            return;
        }

        // Create/Find systems
        var phaseDirector = FindOrCreateComponent(root.transform, "Proto_PhaseDirector", tPhaseDirector);
        var phaseHud      = FindOrCreateComponent(root.transform, "Proto_PhaseHUD", tPhaseHud);

        // Ensure HUD canvas (minimal)
        EnsureHudCanvas(phaseHud.gameObject);

        // Call optional helpers if present
        InvokeIfExists(phaseDirector, "SetInitialPhaseForEditor", new object[] { FindEnumValue("ProtoPhase", "Story") });
        InvokeIfExists(phaseHud, "EnsureMinimalUI", Array.Empty<object>());

        // Proto_UI (optional)
        Component uiComp = null;
        if (tProtoUI != null)
        {
            uiComp = UnityEngine.Object.FindObjectOfType(tProtoUI) as Component;
            if (uiComp == null)
            {
                var uiGo = new GameObject("Proto_UI");
                uiGo.transform.SetParent(root.transform, false);
                uiComp = uiGo.AddComponent(tProtoUI);
            }

            // Bind references by common field names (non-fatal)
            TrySetField(uiComp, "phaseDirector", phaseDirector);
            TrySetField(uiComp, "PhaseDirector", phaseDirector);
            TrySetField(uiComp, "phaseHUD", phaseHud);
            TrySetField(uiComp, "PhaseHUD", phaseHud);
        }

        // Also bind HUD into director if fields exist
        TrySetField(phaseDirector, "hud", phaseHud);
        TrySetField(phaseDirector, "phaseHUD", phaseHud);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(phaseDirector);
        EditorUtility.SetDirty(phaseHud);
        if (uiComp != null) EditorUtility.SetDirty(uiComp);

        EditorUtility.DisplayDialog(
            "One Shot Setup",
            "Phase System の土台を作成/補完しました。\nHierarchy の __PROTO_SYSTEMS__ を確認してください。",
            "OK");
    }

    private static void EnsureEventSystem()
    {
        var es = UnityEngine.Object.FindObjectOfType<EventSystem>();
        if (es != null) return;
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();
    }

    private static Component FindOrCreateComponent(Transform parent, string name, Type t)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
        }

        var comp = go.GetComponent(t);
        if (comp == null) comp = go.AddComponent(t);
        return comp;
    }

    private static void EnsureHudCanvas(GameObject go)
    {
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        if (go.GetComponent<UnityEngine.UI.CanvasScaler>() == null)
            go.AddComponent<UnityEngine.UI.CanvasScaler>();
        if (go.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    private static Type FindTypeByName(string typeName)
    {
        // Try exact first (including global namespace)
        var t = Type.GetType(typeName);
        if (t != null) return t;

        // Search all assemblies
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var match = asm.GetTypes().FirstOrDefault(x => x.Name == typeName);
                if (match != null) return match;
            }
            catch (ReflectionTypeLoadException)
            {
                // ignore
            }
        }
        return null;
    }

    private static object FindEnumValue(string enumTypeName, string valueName)
    {
        var tEnum = FindTypeByName(enumTypeName);
        if (tEnum == null || !tEnum.IsEnum) return null;
        try
        {
            return Enum.Parse(tEnum, valueName);
        }
        catch
        {
            return null;
        }
    }

    private static void InvokeIfExists(Component target, string methodName, object[] args)
    {
        if (target == null) return;
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var m = target.GetType().GetMethod(methodName, flags);
        if (m == null) return;

        try { m.Invoke(target, args); }
        catch { /* ignore */ }
    }

    private static void TrySetField(object instance, string fieldName, object value)
    {
        if (instance == null || value == null) return;
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var f = instance.GetType().GetField(fieldName, flags);
        if (f == null) return;
        if (!f.FieldType.IsAssignableFrom(value.GetType())) return;
        f.SetValue(instance, value);
    }
}
#endif

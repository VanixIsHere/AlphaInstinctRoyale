using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class DebugOverlayController : MonoBehaviour
{
    private enum OverlayMode
    {
        Hidden = 0,
        Basic = 1,
        Verbose = 2,
    }

    [Header("Layout")]
    [SerializeField] private Rect windowRect = new Rect(16f, 16f, 460f, 320f);
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;
    [SerializeField] private int fontSize = 14;

    private readonly List<DebugOverlayModule> modules = new();
    private readonly StringBuilder builder = new(2048);

    private OverlayMode overlayMode = OverlayMode.Hidden;
    private GUIStyle windowStyle;
    private GUIStyle labelStyle;
    private Vector2 scrollPosition;

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            overlayMode = overlayMode switch
            {
                OverlayMode.Hidden => OverlayMode.Basic,
                OverlayMode.Basic => OverlayMode.Verbose,
                _ => OverlayMode.Hidden,
            };
        }
    }

    private void OnGUI()
    {
        if (overlayMode == OverlayMode.Hidden)
        {
            return;
        }

        EnsureStyles();
        RefreshModules();
        windowRect.height = Mathf.Min(Screen.height - 32f, windowRect.height);
        windowRect.width = Mathf.Min(Screen.width - 32f, windowRect.width);
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindowContents, $"Debug Overlay [{overlayMode}]", windowStyle);
    }

    private void DrawWindowContents(int windowId)
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);

        var verbosity = overlayMode == OverlayMode.Verbose
            ? DebugOverlayVerbosity.Verbose
            : DebugOverlayVerbosity.Basic;

        foreach (var module in modules)
        {
            if (module == null || !module.IsEnabledInOverlay)
            {
                continue;
            }

            builder.Clear();
            module.BuildContent(builder, verbosity);
            if (builder.Length == 0)
            {
                continue;
            }

            GUILayout.Label(module.ModuleTitle, labelStyle);
            GUILayout.Label(builder.ToString(), labelStyle);
            GUILayout.Space(10f);
        }

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void RefreshModules()
    {
        modules.Clear();
        GetComponents(modules);
    }

    private void EnsureStyles()
    {
        if (windowStyle == null)
        {
            windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = fontSize,
                richText = true,
            };
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
            };
        }
    }
}

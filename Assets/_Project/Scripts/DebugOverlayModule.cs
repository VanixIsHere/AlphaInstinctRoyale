using System.Text;
using UnityEngine;

public enum DebugOverlayVerbosity
{
    Basic = 1,
    Verbose = 2,
}

[DisallowMultipleComponent]
public abstract class DebugOverlayModule : MonoBehaviour
{
    [SerializeField] private string moduleTitle = "Module";
    [SerializeField] private bool isEnabledInOverlay = true;

    public string ModuleTitle => string.IsNullOrWhiteSpace(moduleTitle) ? GetType().Name : moduleTitle;
    public bool IsEnabledInOverlay => isEnabledInOverlay && isActiveAndEnabled;

    public abstract void BuildContent(StringBuilder builder, DebugOverlayVerbosity verbosity);
}

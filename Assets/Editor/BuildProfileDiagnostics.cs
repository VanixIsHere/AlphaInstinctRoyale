using UnityEditor;
using UnityEngine;
using System.IO;

public class BuildProfileDiagnostics
{
    [MenuItem("Tools/Diagnostics/Check Build Profile")]
    public static void CheckBuildProfile()
    {
        Debug.Log("=== Build Profile Diagnostic Start ===");

        // 1. Check Current Build Target
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
        Debug.Log($"Current Build Target: {target} ({group})");

        if (!BuildPipeline.IsBuildTargetSupported(group, target))
        {
            Debug.LogWarning($"⚠️ Build target {target} is not supported. You may be missing platform modules.");
        }

        // 2. Check EditorUserBuildSettings.asset
        string path = Path.Combine("Library", "EditorUserBuildSettings.asset");
        if (!File.Exists(path))
        {
            Debug.LogWarning("⚠️ Missing EditorUserBuildSettings.asset. Unity may regenerate it, but this could indicate a problem.");
        }
        else
        {
            try
            {
                var content = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(content))
                    Debug.LogWarning("⚠️ EditorUserBuildSettings.asset exists but appears to be empty or corrupted.");
                else
                    Debug.Log("✅ EditorUserBuildSettings.asset found and seems readable.");
            }
            catch (IOException e)
            {
                Debug.LogError($"❌ Failed to read EditorUserBuildSettings.asset: {e.Message}");
            }
        }

        // 3. Check File Permissions
        string libraryFolder = Path.Combine(Directory.GetCurrentDirectory(), "Library");
        if (!Directory.Exists(libraryFolder))
        {
            Debug.LogError("❌ 'Library' folder is missing. Unity needs this to store build settings and other cache data.");
        }
        else
        {
            try
            {
                File.Create(Path.Combine(libraryFolder, "permission_test.tmp")).Close();
                File.Delete(Path.Combine(libraryFolder, "permission_test.tmp"));
                Debug.Log("✅ Write permissions for Library folder are OK.");
            }
            catch
            {
                Debug.LogError("❌ Cannot write to 'Library' folder. Check file system permissions.");
            }
        }

        Debug.Log("=== Build Profile Diagnostic Complete ===");
    }
}

using UnityEditor;
using UnityEngine;
using System.IO;

#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(CreditEntry))]
public class CreditEntryDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        var nameProp = property.FindPropertyRelative("name");
        string entryName = nameProp != null ? nameProp.stringValue : "Unnamed";

        EditorGUI.PropertyField(position, property, new GUIContent(entryName), true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}

public class CreditsExporter {
    [MenuItem("Tools/Export Game Credits")]
    static void ExportGameCredits() {
        var db = Resources.Load<CreditsDatabase>("CreditsDatabase"); // or drag reference in a MonoBehaviour

        if (db == null) {
            Debug.LogError("CreditsDatabase not found in Resources!");
            return;
        }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"Assets/_Project/Credits/Reports/game_credits_{timestamp}.txt";

        using (StreamWriter writer = new StreamWriter(filename)) {
            foreach (var entry in db.entries) {
                writer.WriteLine($"[{entry.category}] {entry.name} ({entry.type})");
                writer.WriteLine($"   Role/Usage: {entry.roleOrUsage}");
                if (!string.IsNullOrEmpty(entry.notes))
                    writer.WriteLine($"   Notes: {entry.notes}");
                writer.WriteLine();
            }
        }

        Debug.Log($"Game credits exported to {filename}");
        Application.OpenURL(filename);
    }
}

#endif
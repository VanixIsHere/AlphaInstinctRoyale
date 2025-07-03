using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using System.IO;
using System.Linq;
using UnityEngine.Localization;
using System.Collections.Generic;

public class LocalizationFontExtractor
{
    [MenuItem("Tools/Localization/Export Text for Font Subsetting")]
    public static void ExportLocalizedText()
    {
        string[] localesToExport = { "en-US", "ja", "ko", "zh-Hant" };
        string[] tableNames = { "UIStrings", "VoiceLineQuotes" };
        string outputDir = "Automation/LocalizationSubsetting/FontSubsetText/";

        Directory.CreateDirectory(outputDir);

        foreach (var code in localesToExport)
        {
            Locale locale = UnityEditor.Localization.LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code == code);
            if (locale == null)
            {
                Debug.LogWarning($"Locale {code} not found.");
                continue;
            }

            string filePath = Path.Combine(outputDir, $"{code}_strings.txt");
            using StreamWriter writer = new(filePath, false);
            HashSet<string> uniqueLines = new();

            foreach (var tableName in tableNames)
            {
                var tableCollection = UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection(tableName);
                var stringTable = tableCollection?.GetTable(locale.Identifier) as StringTable;

                if (stringTable == null)
                {
                    Debug.LogWarning($"Table '{tableName}' not found for locale {code}");
                    continue;
                }

                foreach (var entry in stringTable.Values)
                {
                    var text = entry.LocalizedValue?.Trim();
                    if (!string.IsNullOrEmpty(text) && uniqueLines.Add(text)) // prevent dupes
                    {
                        writer.WriteLine(text);
                    }
                }
            }

            Debug.Log($"✅ Exported {code} text to {filePath}");
        }
    }
}

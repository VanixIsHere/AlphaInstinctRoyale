using UnityEngine.UIElements;

public static class FontScaler
{
    public static void ScaleFonts(VisualElement root, float scale)
    {
        foreach (var el in root.Query<Label>().ToList())
        {
            var originalSize = el.resolvedStyle.fontSize;
            el.style.fontSize = originalSize * scale;
        }

        foreach (var el in root.Query<Button>().ToList())
        {
            var originalSize = el.resolvedStyle.fontSize;
            el.style.fontSize = originalSize * scale;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Components;
using UnityEngine.UIElements;
using TMPro;

public enum UIStringKey
{
    GameTitle,
    Resume,
    Settings,
    Debug,
    QuitGame,
    Audio,
    Video,
    Gameplay,
    Controls,
    Misc,
    MasterVolume,
    MusicVolume,
    SFXVolume,
    VoiceVolume,
    Apply,
    Resolution,
    ScreenMode,
    Fullscreen,
    BorderlessWindowed,
    Windowed
}

public enum FontKey
{
    Header,
    Standard
}

public static class LocalizationHelper
{
    private const string UIStringsTableName = "UIStrings";
    private const string FontTableName = "FontAsset";

    private static readonly Dictionary<UIStringKey, string> UIKeyMap = new()
    {
        { UIStringKey.GameTitle, "GameTitle" },
        { UIStringKey.Resume, "PauseScreen_Resume" },
        { UIStringKey.Settings, "PauseScreen_Settings" },
        { UIStringKey.Debug, "PauseScreen_Debug" },
        { UIStringKey.QuitGame, "PauseScreen_Quit" },
        { UIStringKey.Audio, "Settings_Audio" },
        { UIStringKey.Video, "Settings_Video" },
        { UIStringKey.Gameplay, "Settings_Gameplay" },
        { UIStringKey.Controls, "Settings_Controls" },
        { UIStringKey.Misc, "Settings_Misc" },
        { UIStringKey.MasterVolume, "Audio_MasterVolume" },
        { UIStringKey.MusicVolume, "Audio_MusicVolume" },
        { UIStringKey.SFXVolume, "Audio_SFXVolume" },
        { UIStringKey.VoiceVolume, "Audio_VoiceVolume" },
        { UIStringKey.Apply, "Settings_Apply" },
        { UIStringKey.Resolution, "Video_Resolution" },
        { UIStringKey.ScreenMode, "Video_ScreenMode" },
        { UIStringKey.Fullscreen, "ScreenMode_Fullscreen" },
        { UIStringKey.BorderlessWindowed, "ScreenMode_BorderlessWindowed" },
        { UIStringKey.Windowed, "ScreenMode_Windowed" }
    };

    private static readonly Dictionary<FontKey, string> FontKeyMap = new()
    {
        { FontKey.Header, "Font_Header" },
        { FontKey.Standard, "Font_Standard" }
    };

    public static string GetUIText(UIStringKey key)
    {
        if (!UIKeyMap.TryGetValue(key, out var entryKey))
        {
            Debug.LogWarning($"UIStringKey '{key}' is not mapped to a localization entry.");
            return string.Empty;
        }

        return LocalizationSettings.StringDatabase.GetLocalizedString(UIStringsTableName, entryKey);
    }

    public static void LocalizeTextElement(TextElement element, UIStringKey key, FontKey fontKey = FontKey.Standard)
    {
        if (!UIKeyMap.TryGetValue(key, out var entryKey))
        {
            Debug.LogWarning($"UIStringKey '{key}' is not mapped to a localization entry.");
            return;
        }

        if (!FontKeyMap.TryGetValue(fontKey, out var fontEntry))
        {
            Debug.LogWarning($"FontKey '{fontKey}' is not mapped to a localization entry.");
            fontEntry = FontKeyMap[FontKey.Standard];
        }

        var localizedString = new LocalizedString(UIStringsTableName, entryKey);
        var localizedFont = new LocalizedAsset<Font>(FontTableName, fontEntry);

        void UpdateText(string value) => element.text = value;
        void UpdateFont(Font asset) => element.style.unityFontDefinition = FontDefinition.FromFont(asset);

        localizedString.StringChanged += UpdateText;
        localizedFont.AssetChanged += UpdateFont;

        element.RegisterCallback<DetachFromPanelEvent>(_ =>
        {
            localizedString.StringChanged -= UpdateText;
            localizedFont.AssetChanged -= UpdateFont;
        });

        localizedFont.LoadAssetAsync();
        localizedString.RefreshString();
    }

    public static void LocalizeTMPText(TMP_Text textComponent, UIStringKey key, FontKey fontKey = FontKey.Standard)
    {
        if (!UIKeyMap.TryGetValue(key, out var entryKey))
        {
            Debug.LogWarning($"UIStringKey '{key}' is not mapped to a localization entry.");
            return;
        }

        if (!FontKeyMap.TryGetValue(fontKey, out var fontEntry))
        {
            Debug.LogWarning($"FontKey '{fontKey}' is not mapped to a localization entry.");
            fontEntry = FontKeyMap[FontKey.Standard];
        }

        var localizedString = new LocalizedString(UIStringsTableName, entryKey);
        var localizedFont = new LocalizedAsset<TMP_FontAsset>(FontTableName, fontEntry);

        void UpdateText(string value) => textComponent.text = value;
        void UpdateFont(TMP_FontAsset asset) => textComponent.font = asset;

        localizedString.StringChanged += UpdateText;
        localizedFont.AssetChanged += UpdateFont;

        textComponent.RegisterCallback<DetachFromPanelEvent>(_ =>
        {
            localizedString.StringChanged -= UpdateText;
            localizedFont.AssetChanged -= UpdateFont;
        });

        localizedFont.LoadAssetAsync();
        localizedString.RefreshString();
    }
}

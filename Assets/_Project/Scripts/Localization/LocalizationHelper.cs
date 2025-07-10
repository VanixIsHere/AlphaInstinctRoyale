using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

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

public static class LocalizationHelper
{
    private const string UIStringsTableName = "UIStrings";

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

    public static string GetUIText(UIStringKey key)
    {
        if (!UIKeyMap.TryGetValue(key, out var entryKey))
        {
            Debug.LogWarning($"UIStringKey '{key}' is not mapped to a localization entry.");
            return string.Empty;
        }

        return LocalizationSettings.StringDatabase.GetLocalizedString(UIStringsTableName, entryKey);
    }
}

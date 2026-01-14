using System;

namespace GameSettings
{
    [Flags]
    public enum SettingsSaveMask
    {
        None = 0,
        ScreenResolution = 1 << 0,
        ScreenMode = 1 << 1,
        OverallGraphicsQuality = 1 << 2,
        MasterVolume = 1 << 3,
        MusicVolume = 1 << 4,
        SFXVolume = 1 << 5,
        VoiceVolume = 1 << 6,
        Language = 1 << 7,
        All = ScreenResolution | ScreenMode | OverallGraphicsQuality |
              MasterVolume | MusicVolume | SFXVolume | VoiceVolume |
              Language
    }
}
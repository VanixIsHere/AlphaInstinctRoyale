using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameSettings
{
    public enum ResolutionSetting
    {
        _1024x768,
        _1280x720,
        _1600x900,
        _1920x1080,
        _2560x1440,
        _3840x2160
    }

    public static class ResolutionSettingExtensions
    {
        public static string ToResolutionString(this ResolutionSetting res)
        {
            return res switch
            {
                ResolutionSetting._1024x768 => "1024x768",
                ResolutionSetting._1280x720 => "1280x720",
                ResolutionSetting._1600x900 => "1600x900",
                ResolutionSetting._1920x1080 => "1920x1080",
                ResolutionSetting._2560x1440 => "2560x1440",
                ResolutionSetting._3840x2160 => "3840x2160",
                _ => "Unknown"
            };
        }

        public static ResolutionSetting ToResolutionSetting(string resolutionString)
        {
            return resolutionString switch
            {
                "1024x768" => ResolutionSetting._1024x768,
                "1280x720" => ResolutionSetting._1280x720,
                "1600x900" => ResolutionSetting._1600x900,
                "1920x1080" => ResolutionSetting._1920x1080,
                "2560x1440" => ResolutionSetting._2560x1440,
                "3840x2160" => ResolutionSetting._3840x2160,
                _ => ResolutionSetting._1920x1080 // Default to 1920x1080 if incorrect value is provided
            };
        }

        public static Vector2Int GetDimensions(this ResolutionSetting res)
        {
            return res switch
            {
                ResolutionSetting._1024x768 => new Vector2Int(1024, 768),
                ResolutionSetting._1280x720 => new Vector2Int(1280, 720),
                ResolutionSetting._1600x900 => new Vector2Int(1600, 900),
                ResolutionSetting._1920x1080 => new Vector2Int(1920, 1080),
                ResolutionSetting._2560x1440 => new Vector2Int(2560, 1440),
                ResolutionSetting._3840x2160 => new Vector2Int(3840, 2160),
                _ => new Vector2Int(1920, 1080),
            };
        }

        public static List<string> GetResolutionList()
        {
            List<string> result = new List<string>();
            foreach (ResolutionSetting res in Enum.GetValues(typeof(ResolutionSetting)))
            {
                result.Add(ToResolutionString(res));
            }
            return result;
        }
    }

    public enum ScreenModeSetting
    {
        Fullscreen,
        BorderlessWindow,
        Windowed
    }

    public static class ScreenModeSettingExtensions
    {
        public static string ToScreenModeString(this ScreenModeSetting res)
        {
            return res switch
            {
                ScreenModeSetting.Fullscreen => "Fullscreen",
                ScreenModeSetting.BorderlessWindow => "Borderless Window",
                ScreenModeSetting.Windowed => "Windowed",
                _ => "Unknown"
            };
        }

        public static ScreenModeSetting ToScreenModeSetting(string screenModeString)
        {
            return screenModeString switch
            {
                "Fullscreen" => ScreenModeSetting.Fullscreen,
                "Borderless Window" => ScreenModeSetting.BorderlessWindow,
                "Windowed" => ScreenModeSetting.Windowed,
                _ => ScreenModeSetting.Fullscreen // Default to Fullscreen if incorrect value is provided
            };
        }

        public static List<string> GetScreenModeList()
        {
            List<string> result = new List<string>();
            foreach (ScreenModeSetting res in Enum.GetValues(typeof(ScreenModeSetting)))
            {
                result.Add(ToScreenModeString(res));
            }
            return result;
        }
    }

    public enum GraphicsQuality
    {
        Low,
        Medium,
        High,
        Ultra
    }

    public enum LanguageSetting
    {
        English,
        Japanese,
        Korean,
        ChineseTraditional
    }

    public static class LanguageSettingExtensions
    {
        public static string ToLanguageString(this LanguageSetting lang)
        {
            return lang switch
            {
                LanguageSetting.English => "English",
                LanguageSetting.Japanese => "Japanese",
                LanguageSetting.Korean => "Korean",
                LanguageSetting.ChineseTraditional => "Chinese (Traditional)",
                _ => "English"
            };
        }

        public static string ToLocaleCode(this LanguageSetting lang)
        {
            return lang switch
            {
                LanguageSetting.English => "en-US",
                LanguageSetting.Japanese => "ja",
                LanguageSetting.Korean => "ko",
                LanguageSetting.ChineseTraditional => "zh-Hant",
                _ => "en-US"
            };
        }

        public static LanguageSetting ToLanguageSetting(string languageString)
        {
            return languageString switch
            {
                "English" => LanguageSetting.English,
                "Japanese" => LanguageSetting.Japanese,
                "Korean" => LanguageSetting.Korean,
                "Chinese (Traditional)" => LanguageSetting.ChineseTraditional,
                _ => LanguageSetting.English
            };
        }

        public static List<string> GetLanguageList()
        {
            List<string> result = new List<string>();
            foreach (LanguageSetting lang in Enum.GetValues(typeof(LanguageSetting)))
            {
                result.Add(lang.ToLanguageString());
            }
            return result;
        }
    }
}
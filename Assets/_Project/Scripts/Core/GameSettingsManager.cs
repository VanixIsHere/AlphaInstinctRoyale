using UnityEngine;
using GameSettings;
using System.IO;
using System;
using UnityEngine.Rendering;
using UnityEngine.Localization.Settings;

public class GameSettingsManager : MonoBehaviour
{
    private static readonly string SettingsFileName = "settings.json";
    private string SettingsFilePath => Path.Combine(Application.persistentDataPath, SettingsFileName);

    public static GameSettingsManager Instance { get; private set; }

    /* VIDEO OPTIONS */
    public ResolutionSetting ScreenResolution { get; private set; }
    public ScreenModeSetting ScreenMode { get; private set; }

    /* AUDIO OPTIONS */
    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public float SFXVolume { get; private set; }
    public float VoiceVolume { get; private set; }

    /* LOCALE OPTIONS */
    public LanguageSetting Language { get; private set; }
    public event Action<LanguageSetting> LanguageChanged;

    /* GRAPHICS OPTIONS */
    public GraphicsQuality OverallGraphicsQuality { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    private void SaveSettings()
    {
        var data = new GameSettingsData
        {
            screenResolution = ScreenResolution,
            screenMode = ScreenMode,
            overallGraphicsQuality = OverallGraphicsQuality,
            masterVolume = MasterVolume,
            musicVolume = MusicVolume,
            sfxVolume = SFXVolume,
            voiceVolume = VoiceVolume,
            language = Language,
        };
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SettingsFilePath, json);
    }

    private void LoadSettings()
    {
        Debug.Log($"Settings file path: {SettingsFilePath}");
        if (File.Exists(SettingsFilePath))
        {
            try
            {
                string json = File.ReadAllText(SettingsFilePath);
                var data = JsonUtility.FromJson<GameSettingsData>(json);
                Debug.Log($"Settings JSON: {json}");

                ScreenResolution = data.screenResolution;
                ScreenMode = data.screenMode;
                OverallGraphicsQuality = data.overallGraphicsQuality;
                MasterVolume = data.masterVolume;
                MusicVolume = data.musicVolume;
                SFXVolume = data.sfxVolume;
                VoiceVolume = data.voiceVolume;
                Language = data.language;

                var locale = LocalizationSettings.AvailableLocales.GetLocale(Language.ToLocaleCode());
                if (locale != null)
                    LocalizationSettings.SelectedLocale = locale;
                LanguageChanged?.Invoke(Language);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load settings. Using defaults. Reason: {e.Message}");
                SetDefaults();
            }
        }
        else
        {
            // Settings should take default values if settings file doesn't exist.
            SetDefaults();
        }
    }

    private void SetDefaults()
    {
        ScreenResolution = ResolutionSetting._1920x1080; // TODO: Detect appropriate screen resolution?
        ScreenMode = ScreenModeSetting.Fullscreen;
        OverallGraphicsQuality = GraphicsQuality.High; // TODO: Detect appropriate graphics quality?
        MasterVolume = 1f;
        MusicVolume = 1f;
        SFXVolume = 1f;
        VoiceVolume = 1f;
        Language = LanguageSetting.English;

        var locale = LocalizationSettings.AvailableLocales.GetLocale(Language.ToLocaleCode());
        if (locale != null)
            LocalizationSettings.SelectedLocale = locale;
        LanguageChanged?.Invoke(Language);
        SaveSettings();
    }

    public void SetScreenResolution(ResolutionSetting res)
    {
        ScreenResolution = res;
        SaveSettings();
    }

    public void SetScreenMode(ScreenModeSetting mode)
    {
        ScreenMode = mode;
        SaveSettings();
    }

    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        SaveSettings();
    }

    public void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp01(volume);
        SaveSettings();
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = Mathf.Clamp01(volume);
        SaveSettings();
    }

    public void SetVoiceVolume(float volume)
    {
        VoiceVolume = Mathf.Clamp01(volume);
        SaveSettings();
    }

    public void SetOverallGraphicsQuality(GraphicsQuality quality)
    {
        OverallGraphicsQuality = quality;
        SaveSettings();
    }

    public void SetLanguage(LanguageSetting lang)
    {
        Language = lang;
        var locale = LocalizationSettings.AvailableLocales.GetLocale(lang.ToLocaleCode());
        if (locale != null)
            LocalizationSettings.SelectedLocale = locale;
        LanguageChanged?.Invoke(lang);
        SaveSettings();
    }
}

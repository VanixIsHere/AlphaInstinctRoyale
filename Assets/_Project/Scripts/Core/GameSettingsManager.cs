using UnityEngine;
using GameSettings;
using System.IO;
using System;
using UnityEngine.Rendering;
using UnityEngine.Localization.Settings;
using UnityEngine.Audio;

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

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer mainAudioMixer;

    private const string MasterVolumeParameter = "MasterVolume";
    private const string MusicVolumeParameter = "MusicVolume";
    private const string SfxVolumeParameter = "SFXVolume";

    /* LOCALE OPTIONS */
    public LanguageSetting Language { get; private set; }
    public event Action<LanguageSetting> LanguageChanged;

    /* GRAPHICS OPTIONS */
    public GraphicsQuality OverallGraphicsQuality { get; private set; }

    private FullScreenMode ToUnityScreenMode(ScreenModeSetting mode)
    {
        return mode switch
        {
            ScreenModeSetting.Fullscreen => FullScreenMode.ExclusiveFullScreen,
            ScreenModeSetting.BorderlessWindow => FullScreenMode.FullScreenWindow,
            ScreenModeSetting.Windowed => FullScreenMode.Windowed,
            _ => FullScreenMode.FullScreenWindow
        };
    }

    private void ApplyVideoSettings()
    {
        Vector2Int dims = ScreenResolution.GetDimensions();
        Screen.SetResolution(dims.x, dims.y, ToUnityScreenMode(ScreenMode));
    }

    private void Awake()
    {
        Debug.Log($"[{name}] Scene: {gameObject.scene.name} | Is persistent: {gameObject.scene.name == "DontDestroyOnLoad"}");

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    public void SaveSettings(SettingsSaveMask mask = SettingsSaveMask.All)
    {
        GameSettingsData data;
        if (mask != SettingsSaveMask.All && File.Exists(SettingsFilePath))
        {
            try
            {
                string existing = File.ReadAllText(SettingsFilePath);
                data = JsonUtility.FromJson<GameSettingsData>(existing);
            }
            catch
            {
                data = new GameSettingsData();
            }
        }
        else
        {
            data = new GameSettingsData();
        }

        if (mask.HasFlag(SettingsSaveMask.ScreenResolution))
            data.screenResolution = ScreenResolution;
        if (mask.HasFlag(SettingsSaveMask.ScreenMode))
            data.screenMode = ScreenMode;
        if (mask.HasFlag(SettingsSaveMask.OverallGraphicsQuality))
            data.overallGraphicsQuality = OverallGraphicsQuality;
        if (mask.HasFlag(SettingsSaveMask.MasterVolume))
            data.masterVolume = MasterVolume;
        if (mask.HasFlag(SettingsSaveMask.MusicVolume))
            data.musicVolume = MusicVolume;
        if (mask.HasFlag(SettingsSaveMask.SFXVolume))
            data.sfxVolume = SFXVolume;
        if (mask.HasFlag(SettingsSaveMask.VoiceVolume))
            data.voiceVolume = VoiceVolume;
        if (mask.HasFlag(SettingsSaveMask.Language))
            data.language = Language;

        string json = JsonUtility.ToJson(data, true);
        Console.Write($"In SaveSettings: {data}");
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
                ApplyVideoSettings();
                ApplyAudioSettings();
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
        ApplyVideoSettings();
        ApplyAudioSettings();
        SaveSettings();
    }

    public void SetScreenResolution(ResolutionSetting res)
    {
        ScreenResolution = res;
        ApplyVideoSettings();
    }

    public void SetScreenMode(ScreenModeSetting mode)
    {
        ScreenMode = mode;
        ApplyVideoSettings();
    }

    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        ApplyAudioSettings();
    }

    public void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp01(volume);
        ApplyAudioSettings();
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = Mathf.Clamp01(volume);
        ApplyAudioSettings();
    }

    public void SetVoiceVolume(float volume)
    {
        VoiceVolume = Mathf.Clamp01(volume);
    }

    public void SetOverallGraphicsQuality(GraphicsQuality quality)
    {
        OverallGraphicsQuality = quality;
    }

    public void SetLanguage(LanguageSetting lang)
    {
        Language = lang;
        var locale = LocalizationSettings.AvailableLocales.GetLocale(lang.ToLocaleCode());
        if (locale != null)
            LocalizationSettings.SelectedLocale = locale;
        LanguageChanged?.Invoke(lang);
    }

    private void ApplyAudioSettings()
    {
        if (mainAudioMixer == null)
        {
            return;
        }

        mainAudioMixer.SetFloat(MasterVolumeParameter, ToMixerDecibels(MasterVolume));
        mainAudioMixer.SetFloat(MusicVolumeParameter, ToMixerDecibels(MusicVolume));
        mainAudioMixer.SetFloat(SfxVolumeParameter, ToMixerDecibels(SFXVolume));
    }

    private static float ToMixerDecibels(float normalizedVolume)
    {
        if (normalizedVolume <= 0.0001f)
        {
            return -80f;
        }

        return Mathf.Log10(normalizedVolume) * 20f;
    }
}

using GameSettings;

[System.Serializable]
public class GameSettingsData
{
    public ResolutionSetting screenResolution;
    public ScreenModeSetting screenMode;
    public GraphicsQuality overallGraphicsQuality;
    public float masterVolume;
    public float musicVolume;
    public float sfxVolume;
    public float voiceVolume;
    public LanguageSetting language;
}
using UnityEngine;

public static class Env
{
    static EnvironmentalConfig _cfg;

    static EnvironmentalConfig Config
    {
        get
        {
            if (_cfg == null)
            {
                _cfg = Resources.Load<EnvironmentalConfig>("EnvironmentConfig");
                _cfg.Initialize();
            }
            return _cfg;
        }
    }

    public static string MatchmakingUrlBase => Config.MatchmakingUrlBase;
}
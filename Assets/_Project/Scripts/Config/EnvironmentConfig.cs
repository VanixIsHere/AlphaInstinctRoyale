using UnityEngine;

[CreateAssetMenu(fileName = "EnvironmentalConfig", menuName = "Config/Environment")]
public class EnvironmentalConfig : ScriptableObject
{
    [HideInInspector] public string MatchmakingUrlBase;

    // Called once to set it up based on the environment
    public void Initialize()
    {
#if UNITY_EDITOR
        MatchmakingUrlBase = "http://localhost:7084";
#else
        if (Debug.isDebugBuild)
        {
            MatchmakingUrlBase = "https://staging.api.example.com";
        }
        else
        {
            MatchmakingUrlBase = "https://api.example.com";
        }
#endif
    }
}
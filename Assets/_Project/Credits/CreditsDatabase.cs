using UnityEngine;
using System.Collections.Generic;

public enum CreditCategory {
    LeadDev,
    Programming,
    Art,
    Audio,
    QA,
    Writing,
    Marketing,
    SocialMedia,
    Publishing,
    Localisation,
    ExternalStudio,
    Middleware,
    SoftwareTool,
    Asset,
    SpecialThanks,
    Other
}

public enum CreditType {
    Individual,
    Team,
    Software,
    Other
}

[CreateAssetMenu(fileName = "CreditsDatabase", menuName = "Credits/CreditsDatabase")]
public class CreditsDatabase : ScriptableObject {
    public List<CreditEntry> entries = new List<CreditEntry>();
}

[System.Serializable]
public class CreditEntry {
    public CreditCategory category;
    public CreditType type;
    public string name;           // Person, Studio, or Software name
    public string altname;        // In case a they care to go by a username.
    public Texture2D imageOrLogo;
    public string roleOrUsage;    // Role (for people/teams) OR usage (for software)
    [TextArea]
    public string notes;          // Optional extra details or shoutout
}
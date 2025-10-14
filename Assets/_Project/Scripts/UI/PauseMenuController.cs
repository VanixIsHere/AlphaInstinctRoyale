using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GameSettings;

public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }
    public GameSettingsManager GameSettingsManager;
    public VisualTreeAsset InGameMenuContainer;
    public VisualTreeAsset InGameLocalizationToggle;
    private bool isPaused = false;

    // Runtime state
    private Camera mainCamera;
    private HandleCursor cursor;
    private UIBlockerManager uiBlocker;
    private ModalManager modal;
    private UIDocument rootDoc;
    private VisualElement menuRoot;
    private readonly List<string> openItemIds = new List<string>();
    private readonly List<VisualElement> openLayers = new List<VisualElement>();

    void Awake()
    {
        Instance = this;
        rootDoc = GetComponent<UIDocument>();
        if (rootDoc != null)
            rootDoc.rootVisualElement.style.display = DisplayStyle.None;
        mainCamera = Camera.main;
    }

    void Start()
    {
        cursor = mainCamera.GetComponent<HandleCursor>();
        uiBlocker = GetComponent<UIBlockerManager>();
        modal = GetComponent<ModalManager>();
        GroupContainerMenuItem.SetModalManager(modal);

        BuildMenu();
        GameSettingsManager.LanguageChanged += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        GameSettingsManager.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(LanguageSetting lang)
    {
        UpdateLanguageDropdown(lang);
    }

    private DropdownSetting languageSetting;
    private DropdownSetting resolutionSetting;

    private void ResetMenuState()
    {
        BuildMenu();
    }

    private void BuildMenu()
    {
        var root = rootDoc.rootVisualElement.Q<VisualElement>("ROOT");
        root.Clear();

        var menuContainer = InGameMenuContainer.CloneTree();
        var elementContent = menuContainer.Q<VisualElement>("ui-element-content");
        root.Add(menuContainer);

        if (InGameLocalizationToggle != null)
        {
            rootDoc.rootVisualElement.Add(InGameLocalizationToggle.CloneTree());
        }

        menuRoot = elementContent;
        openLayers.Clear();
        openItemIds.Clear();
        openLayers.Add(menuRoot);
        UIUtils.AdjustColumnFlex(menuRoot.parent);

        if (languageSetting != null)
            GameSettingsManager.LanguageChanged -= UpdateLanguageDropdown;

        languageSetting = new DropdownSetting(
            "Language",
            LanguageSettingExtensions.GetLanguageList(),
            LanguageSettingExtensions.ToLanguageString(GameSettingsManager.Language),
            val =>
            {
                var prev = GameSettingsManager.Language;
                var chosen = LanguageSettingExtensions.ToLanguageSetting(val);
                if (prev == chosen)
                    return;

                GameSettingsManager.SetLanguage(chosen);
                modal.ShowTimedConfirm(
                    "Keep language?",
                    "Reverting if not confirmed.",
                    () =>
                    {
                        languageSetting?.Apply();
                        GroupContainerMenuItem.ClearPendingChanges(SettingsSaveMask.Language);
                        GameSettingsManager.SaveSettings(SettingsSaveMask.Language);
                    },
                    () =>
                    {
                        GameSettingsManager.SetLanguage(prev);
                        GroupContainerMenuItem.ClearPendingChanges(SettingsSaveMask.Language);
                    });
            }, SettingsSaveMask.Language);

        GameSettingsManager.LanguageChanged += UpdateLanguageDropdown;

        var rootMenu = new List<IMenuItem>
        {
            new LeafMenuItem("resume", "Resume", ResumeGame, null, UIStringKey.Resume),
            new Submenu("settings", UIStringKey.Settings, "tier1-button",
                new GroupContainerMenuItem("audio", UIStringKey.Audio, "",
                    new SliderSetting(UIStringKey.MasterVolume, 0f, 100f, GameSettingsManager.MasterVolume*100,
                        v => { GameSettingsManager.SetMasterVolume(v/100); }, SettingsSaveMask.MasterVolume),
                    new SliderSetting(UIStringKey.MusicVolume, 0f, 100f, GameSettingsManager.MusicVolume*100,
                        v => { GameSettingsManager.SetMusicVolume(v/100); }, SettingsSaveMask.MusicVolume),
                    new SliderSetting(UIStringKey.SFXVolume, 0f, 100f, GameSettingsManager.SFXVolume*100,
                        v => { GameSettingsManager.SetSFXVolume(v/100); }, SettingsSaveMask.SFXVolume),
                    new SliderSetting(UIStringKey.VoiceVolume, 0f, 100f, GameSettingsManager.VoiceVolume*100,
                        v => { GameSettingsManager.SetVoiceVolume(v/100); }, SettingsSaveMask.VoiceVolume)
                ),
                new GroupContainerMenuItem("video", UIStringKey.Video, "",
                    resolutionSetting = new DropdownSetting(
                        UIStringKey.Resolution,
                        ResolutionSettingExtensions.GetResolutionList(),
                        ResolutionSettingExtensions.ToResolutionString(GameSettingsManager.ScreenResolution),
                        val =>
                        {
                            var prev = GameSettingsManager.ScreenResolution;
                            var chosen = ResolutionSettingExtensions.ToResolutionSetting(val);
                            if (prev == chosen)
                                return;

                            GameSettingsManager.SetScreenResolution(chosen);
                            modal.ShowTimedConfirm(
                                "Keep resolution?",
                                "Reverting if not confirmed.",
                                () => {
                                    resolutionSetting?.Apply();
                                    GroupContainerMenuItem.ClearPendingChanges(SettingsSaveMask.ScreenResolution);
                                    GameSettingsManager.SaveSettings(SettingsSaveMask.ScreenResolution);
                                },
                                () =>
                                {
                                    resolutionSetting?.SetDisplayValue(ResolutionSettingExtensions.ToResolutionString(prev));
                                    GameSettingsManager.SetScreenResolution(prev);
                                    GroupContainerMenuItem.ClearPendingChanges(SettingsSaveMask.ScreenResolution);
                                });
                        }, SettingsSaveMask.ScreenResolution),
                    new DropdownSetting(
                        UIStringKey.ScreenMode,
                        ScreenModeSettingExtensions.GetScreenModeList(),
                        ScreenModeSettingExtensions.ToScreenModeString(GameSettingsManager.ScreenMode),
                        val => { GameSettingsManager.SetScreenMode(ScreenModeSettingExtensions.ToScreenModeSetting(val)); }, SettingsSaveMask.ScreenMode)
                ),
                new GroupContainerMenuItem("gameplay", UIStringKey.Gameplay, "",
                    new ToggleSetting("<filler option gameplay>", true, b => Debug.Log("Useless: " + b))
                ),
                new GroupContainerMenuItem("controls", UIStringKey.Controls, "",
                    new ToggleSetting("<filler option controls>", true, b => Debug.Log("Useless: " + b))
                ),
                new GroupContainerMenuItem("misc", UIStringKey.Misc, "",
                    languageSetting,
                    new ToggleSetting("<filler option misc>", true, b => Debug.Log("Useless: " + b))
                )
            ),
            new Submenu("debug", UIStringKey.Debug, null,
                new LeafMenuItem("matchmaking", "Enter matchmaking", HandleAttemptMatchmaking)),
            new LeafMenuItem("quit", "Quit Game", HandleQuit, null, UIStringKey.QuitGame)
        };

        foreach (var item in rootMenu)
        {
            var btn = new Button(() =>
            {
                item.OnClick(elementContent, null, 2);
            });
            if (item.LocalizationKey.HasValue)
                LocalizationHelper.LocalizeTextElement(btn, item.LocalizationKey.Value, FontKey.Header);
            else
                btn.text = item.Label;

            btn.AddToClassList("menu-button");
            btn.AddToClassList("tier1-button");

            if (!string.IsNullOrEmpty(item.StyleClass))
                btn.AddToClassList(item.StyleClass);

            elementContent.Add(btn);
        }
    }

    private void UpdateLanguageDropdown(LanguageSetting lang)
    {
        if (languageSetting != null)
        {
            languageSetting.SetDisplayValue(LanguageSettingExtensions.ToLanguageString(lang));
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isPaused && GroupContainerMenuItem.ActivePageHasPending)
        {
            GroupContainerMenuItem.ShowUnsavedPrompt(() => TogglePauseInternal());
            return;
        }

        TogglePauseInternal();
    }

    private void TogglePauseInternal()
    {
        bool wasPaused = isPaused;
        isPaused = !isPaused;

        if (rootDoc != null)
        {
            var root = rootDoc.rootVisualElement;
            root.style.display = isPaused ? DisplayStyle.Flex : DisplayStyle.None;

            if (isPaused)
            {
                // float scale = Screen.height / 1080f;
                // FontScaler.ScaleFonts(root, scale);
            }
        }

        bool shouldPauseTime = GameManager.Instance != null && !GameManager.Instance.IsGameActive; // Don't freeze gameplay if in an active session
        Time.timeScale = shouldPauseTime && isPaused ? 0f : 1f;  // Pause or resume game time

        uiBlocker?.SetBlocking(isPaused);
        cursor.SetState(CursorState.Normal);

        if (!isPaused && wasPaused)
            ResetMenuState();
    }

    // Optional resume button hook
    public void ResumeGame()
    {
        if (GroupContainerMenuItem.ActivePageHasPending)
        {
            GroupContainerMenuItem.ShowUnsavedPrompt(() => ResumeGameInternal());
            return;
        }

        ResumeGameInternal();
    }

    private void ResumeGameInternal()
    {
        isPaused = false;

        if (rootDoc != null)
            rootDoc.rootVisualElement.style.display = DisplayStyle.None;

        Time.timeScale = 1f;

        uiBlocker?.SetBlocking(isPaused);
        cursor.SetState(CursorState.Normal);

        ResetMenuState();
    }

    private void HandleAttemptMatchmaking()
    {
        var mmClient = gameObject.GetComponent<MatchmakerClient>();
        if (mmClient)
        {
            mmClient.JoinMatchmaking();
        }
    }

    private void HandleQuit()
    {
        System.Action showQuit = () =>
        {
            modal.ShowConfirm(
                "Are you sure you want to leave?",
                "Any unsaved changes will be lost.",
                Application.Quit,
                () => Debug.Log("Cancelled Quit"),
                "Yes",
                "No"
            );
        };

        if (GroupContainerMenuItem.ActivePageHasPending)
            GroupContainerMenuItem.ShowUnsavedPrompt(showQuit);
        else
            showQuit();
    }

    public VisualElement TryOpen(IMenuItem item, VisualElement parent, int tier)
    {
        VisualElement nextLayer = null;

        if (openItemIds.Count >= tier - 1 && openItemIds[tier - 2] == item.Id)
        {
            // Already viewing this section; ignore the request
            return null;
        }

        System.Action openAction = () =>
        {
            GroupContainerMenuItem.ClearActivePage();
            nextLayer = UIUtils.CreateOrGetLayerColumn(parent, tier);

            while (openLayers.Count >= tier)
            {
                openLayers.RemoveAt(openLayers.Count - 1);
                openItemIds.RemoveAt(openItemIds.Count - 1);
            }

            openLayers.Add(nextLayer);
            openItemIds.Add(item.Id);
        };

        if (GroupContainerMenuItem.ActivePageHasPending)
            GroupContainerMenuItem.ShowUnsavedPrompt(openAction);
        else
            openAction();

        return nextLayer;
    }

    public void CloseTier(int tier)
    {
        if (tier <= 1 || openLayers.Count < tier)
            return;

        System.Action closeAction = () =>
        {
            var parent = openLayers[tier - 2];
            var parentContainer = parent.parent;
            int currentIndex = parentContainer.IndexOf(parent);
            int targetIndex = currentIndex + 1;
            for (int i = parentContainer.childCount - 1; i >= targetIndex; i--)
            {
                var layerToRemove = parentContainer[i];
                layerToRemove.RemoveFromClassList("active");
                layerToRemove.schedule.Execute(() =>
                {
                    parentContainer.Remove(layerToRemove);
                }).ExecuteLater(300);
            }

            UIUtils.AdjustColumnFlex(parentContainer);

            for (int i = openLayers.Count - 1; i >= tier - 1; i--)
            {
                openLayers.RemoveAt(i);
            }
            for (int i = openItemIds.Count - 1; i >= tier - 2; i--)
            {
                openItemIds.RemoveAt(i);
            }

            GroupContainerMenuItem.ClearActivePage();
        };

        if (GroupContainerMenuItem.ActivePageHasPending)
            GroupContainerMenuItem.ShowUnsavedPrompt(closeAction);
        else
            closeAction();
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GameSettings;

public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }
    public GameSettingsManager GameSettingsManager;
    public VisualTreeAsset InGameMenuContainer;
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

        var menuContainer = InGameMenuContainer.CloneTree();
        var root = rootDoc.rootVisualElement.Q<VisualElement>("ROOT");

        var elementContent = menuContainer.Q<VisualElement>("ui-element-content");
        root.Add(menuContainer);

        menuRoot = elementContent;
        openLayers.Add(menuRoot);
        UIUtils.AdjustColumnFlex(menuRoot.parent);

        var languageSetting = new DropdownSetting(
            "Language",
            LanguageSettingExtensions.GetLanguageList(),
            LanguageSettingExtensions.ToLanguageString(GameSettingsManager.Language),
            val => {
                GameSettingsManager.SetLanguage(LanguageSettingExtensions.ToLanguageSetting(val));
            });

        GameSettingsManager.LanguageChanged += lang =>
        {
            languageSetting.SetValue(LanguageSettingExtensions.ToLanguageString(lang));
        };

        var rootMenu = new List<IMenuItem>
        {
            new LeafMenuItem("resume", "Resume", ResumeGame),
            new Submenu("settings", "Settings", "tier1-button",
                new GroupContainerMenuItem("audio", "Audio", "",
                    new SliderSetting("Master Volume", 0f, 100f, GameSettingsManager.MasterVolume*100,
                        v =>
                        {
                            Debug.Log("Master Volume: " + v + " " + v/100);
                            GameSettingsManager.SetMasterVolume(v/100);
                        }),
                    new SliderSetting("Music Volume", 0f, 100f, GameSettingsManager.MusicVolume*100,
                        v =>
                        {
                            Debug.Log("Music Volume: " + v + " " + v/100);
                            GameSettingsManager.SetMusicVolume(v/100);
                        }),
                    new SliderSetting("SFX Volume", 0f, 100f, GameSettingsManager.SFXVolume*100,
                        v =>
                        {
                            Debug.Log("SFX Volume: " + v + " " + v/100);
                            GameSettingsManager.SetSFXVolume(v/100);
                        }),
                    new SliderSetting("Voice Volume", 0f, 100f, GameSettingsManager.VoiceVolume*100,
                        v =>
                        {
                            Debug.Log("Voice Volume: " + v + " " + v/100);
                            GameSettingsManager.SetVoiceVolume(v/100);
                        })
                ),
                new GroupContainerMenuItem("video", "Video", "",
                    new DropdownSetting(
                        "Resolution",
                        ResolutionSettingExtensions.GetResolutionList(),
                        ResolutionSettingExtensions.ToResolutionString(GameSettingsManager.ScreenResolution),
                        val => {
                            Debug.Log("ScreenResolution: " + val);
                            GameSettingsManager.SetScreenResolution(ResolutionSettingExtensions.ToResolutionSetting(val));
                        }),
                    new DropdownSetting(
                        "Screen Mode",
                        ScreenModeSettingExtensions.GetScreenModeList(),
                        ScreenModeSettingExtensions.ToScreenModeString(GameSettingsManager.ScreenMode),
                        val => {
                            Debug.Log("ScreenMode: " + val);
                            GameSettingsManager.SetScreenMode(ScreenModeSettingExtensions.ToScreenModeSetting(val));
                        })
                ),
                new GroupContainerMenuItem("gameplay", "Gameplay", "",
                    new ToggleSetting("<filler option gameplay>", true, b => Debug.Log("Useless: " + b))
                ),
                new GroupContainerMenuItem("controls", "Controls", "",
                    new ToggleSetting("<filler option controls>", true, b => Debug.Log("Useless: " + b))
                ),
                new GroupContainerMenuItem("misc", "Misc", "",
                    languageSetting,
                    new ToggleSetting("<filler option misc>", true, b => Debug.Log("Useless: " + b))
                )
            ),
            new Submenu("debug", "Debug",
                new LeafMenuItem("matchmaking", "Enter matchmaking", HandleAttemptMatchmaking)
            ),
            new LeafMenuItem("quit", "Quit Game", HandleQuit)
        };

        foreach (var item in rootMenu)
        {
            var btn = new Button(() =>
            {
                item.OnClick(elementContent, null, 2);
            })
            { text = item.Label };

            btn.AddToClassList("menu-button");
            btn.AddToClassList("tier1-button");

            if (!string.IsNullOrEmpty(item.StyleClass))
                btn.AddToClassList(item.StyleClass);

            elementContent.Add(btn);
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
    }

    // Optional resume button hook
    public void ResumeGame()
    {
        isPaused = false;

        if (rootDoc != null)
            rootDoc.rootVisualElement.style.display = DisplayStyle.None;

        Time.timeScale = 1f;

        uiBlocker?.SetBlocking(isPaused);
        cursor.SetState(CursorState.Normal);
    }

    private void ExitLeafNode()
    {

    }

    private void OpenAudioSettings()
    {

    }

    private void OpenVideoSettings()
    {

    }

    private void HandleAttemptMatchmaking()
    {

    }

    private void HandleQuit()
    {
        modal.ShowConfirm(
            "Are you sure you want to leave?",
            "Any unsaved changes will be lost.",
            Application.Quit,
            () => Debug.Log("Cancelled Quit"),
            "Yes",
            "No"
        );
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

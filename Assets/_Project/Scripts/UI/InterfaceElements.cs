using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public interface IMenuItem
{
    string Id { get; }
    string Label { get; }
    UIStringKey? LocalizationKey { get; }
    string StyleClass { get; }
    void OnClick(VisualElement parentLayer, VisualElement nextLayer, int tier);
}

public interface ISettingItem
{
    VisualElement Build();
    void Apply();
    void Discard();
}

public interface ISettingsPage
{
    bool HasPendingChanges { get; }
    void Apply();
    void DiscardChanges();
    VisualElement Build();
}

/*
    The submenu is a unique IMenuItem that renders a submenu of buttons, most commonly a GroupContainerMenuItem.
    The 'onClick()' function is built in and cannot be specified.
*/
public class Submenu : IMenuItem
{
    public string Id { get; }
    public string Label => LocalizationKey.HasValue ? LocalizationHelper.GetUIText(LocalizationKey.Value) : string.Empty;
    public UIStringKey? LocalizationKey { get; }
    public string StyleClass { get; }

    public List<IMenuItem> Children { get; }

    public Submenu(string id, UIStringKey localizationKey, string styleClass = null, params IMenuItem[] children)
    {
        Id = id;
        StyleClass = styleClass;
        Children = new List<IMenuItem>(children);
        LocalizationKey = localizationKey;
    }

    public void OnClick(VisualElement parentLayer, VisualElement nextLayer, int tier)
    {
        var layer = PauseMenuController.Instance.TryOpen(this, parentLayer, tier);
        if (layer == null)
            return;

        layer.Clear();

        foreach (var child in Children)
        {
            var btn = new Button(() =>
            {
                child.OnClick(layer, null, tier + 1);
            });
            if (child.LocalizationKey.HasValue)
                LocalizationHelper.LocalizeTextElement(btn, child.LocalizationKey.Value, FontKey.Header);
            else
                btn.text = child.Label;

            btn.AddToClassList("menu-button");
            btn.AddToClassList($"tier{tier}-button");

            if (!string.IsNullOrEmpty(child.StyleClass))
                btn.AddToClassList(child.StyleClass);

            layer.Add(btn);
        }
    }

    // Submenu does not represent a settings page so no Build implementation
}

public class LeafMenuItem : IMenuItem
{
    public string Id { get; }
    public string Label { get; }
    public UIStringKey? LocalizationKey => localizationKey;
    public string StyleClass { get; }

    private readonly System.Action onClickAction;
    private readonly UIStringKey? localizationKey;

    public LeafMenuItem(string id, string label, System.Action onClick, string styleClass = null, UIStringKey? localizationKey = null)
    {
        Id = id;
        Label = label;
        onClickAction = onClick;
        StyleClass = styleClass;
        this.localizationKey = localizationKey;
    }

    public void OnClick(VisualElement parentLayer, VisualElement nextLayer, int tier)
    {
        System.Action run = () => { onClickAction?.Invoke(); };

        if (GroupContainerMenuItem.ActivePageHasPending)
            GroupContainerMenuItem.ShowUnsavedPrompt(run);
        else
            run();
    }
}

public class GroupContainerMenuItem : IMenuItem, ISettingsPage
{
    public string Id { get; }
    public string Label => LocalizationKey.HasValue ? LocalizationHelper.GetUIText(LocalizationKey.Value) : string.Empty;
    public UIStringKey? LocalizationKey { get; }
    public string StyleClass { get; }

    private readonly List<ISettingItem> _settings;
    private static ModalManager modal;
    private static GroupContainerMenuItem activePage;
    private bool dirty;

    public static GroupContainerMenuItem ActivePage => activePage;
    public static bool ActivePageHasPending => activePage != null && activePage.dirty;

    public static void ShowUnsavedPrompt(System.Action onContinue)
    {
        if (activePage == null || modal == null)
        {
            onContinue?.Invoke();
            return;
        }

        modal.ShowConfirm(
            "Apply changes?",
            "You have unsaved settings.",
            () => { activePage.Apply(); onContinue?.Invoke(); },
            () => { activePage.DiscardChanges(); onContinue?.Invoke(); },
            LocalizationHelper.GetUIText(UIStringKey.Apply),
            "Discard"
        );
    }

    public static void SetModalManager(ModalManager mgr)
    {
        modal = mgr;
    }

    internal static void ClearActivePage()
    {
        activePage = null;
    }

    internal static void NotifyChange()
    {
        if (activePage != null)
            activePage.dirty = true;
    }

    internal static void ClearPendingChanges()
    {
        if (activePage != null)
            activePage.dirty = false;
    }

    public GroupContainerMenuItem(string id, UIStringKey localizationKey, string styleClass = null, params ISettingItem[] settings)
    {
        Id = id;
        LocalizationKey = localizationKey;
        StyleClass = styleClass;
        _settings = new List<ISettingItem>(settings);
    }

    public VisualElement Build()
    {
        var root = new VisualElement();
        foreach (var setting in _settings)
        {
            root.Add(setting.Build());
        }
        var apply = new Button(() => { Apply(); });
        LocalizationHelper.LocalizeTextElement(apply, UIStringKey.Apply, FontKey.Header);
        apply.AddToClassList("apply-button");
        root.Add(apply);
        return root;
    }

    public void OnClick(VisualElement parentLayer, VisualElement nextLayer, int tier)
    {
        var layer = PauseMenuController.Instance.TryOpen(this, parentLayer, tier);
        if (layer == null)
            return;

        layer.Clear();

        var scroll = new ScrollView();
        scroll.AddToClassList("settings-scroll");
        layer.Add(scroll);

        var content = Build();
        scroll.Add(content);

        activePage = this;
        dirty = false;
    }

    public bool HasPendingChanges => dirty;

    public void Apply()
    {
        foreach (var setting in _settings)
            setting.Apply();

        PauseMenuController.Instance.GameSettingsManager.SaveSettings();

        dirty = false;
        Debug.Log($"Applied settings for {Label}");
    }

    public void DiscardChanges()
    {
        foreach (var setting in _settings)
            setting.Discard();

        dirty = false;
        Debug.Log($"Discarded settings for {Label}");
    }
}

public class ToggleSetting : ISettingItem
{
    private readonly UIStringKey? labelKey;
    private readonly string label;
    private bool initialValue;
    private bool currentValue;
    private readonly System.Action<bool> onChanged;

    private Toggle toggle;

    public ToggleSetting(UIStringKey labelKey, bool initialValue, System.Action<bool> onChanged)
    {
        this.labelKey = labelKey;
        this.label = null;
        this.initialValue = initialValue;
        this.currentValue = initialValue;
        this.onChanged = onChanged;
    }

    public ToggleSetting(string label, bool initialValue, System.Action<bool> onChanged)
    {
        this.labelKey = null;
        this.label = label;
        this.initialValue = initialValue;
        this.currentValue = initialValue;
        this.onChanged = onChanged;
    }

    public VisualElement Build()
    {
        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.justifyContent = Justify.SpaceBetween;

        var lbl = new Label();
        if (labelKey.HasValue)
            LocalizationHelper.LocalizeTextElement(lbl, labelKey.Value);
        else
            lbl.text = label;
        toggle = new Toggle { value = initialValue };

        toggle.RegisterValueChangedCallback(evt => {
            currentValue = evt.newValue;
            onChanged?.Invoke(evt.newValue);
            GroupContainerMenuItem.NotifyChange();
        });

        container.Add(lbl);
        container.Add(toggle);
        return container;
    }

    public void Apply()
    {
        initialValue = currentValue;
        onChanged?.Invoke(currentValue);
    }

    public void Discard()
    {
        currentValue = initialValue;
        if (toggle != null)
            toggle.value = initialValue;
        onChanged?.Invoke(initialValue);
    }
}

public class DropdownSetting : ISettingItem
{
    private readonly UIStringKey? labelKey;
    private readonly string label;
    private readonly List<string> options;
    private string initialValue;
    private string currentValue;
    private readonly System.Action<string> onChanged;

    private DropdownField dropdownField;

    public DropdownSetting(UIStringKey labelKey, List<string> options, string initialValue, System.Action<string> onChanged)
    {
        this.labelKey = labelKey;
        this.label = null;
        this.options = options;
        this.initialValue = initialValue;
        this.currentValue = initialValue;
        this.onChanged = onChanged;
    }

    public DropdownSetting(string label, List<string> options, string initialValue, System.Action<string> onChanged)
    {
        this.labelKey = null;
        this.label = label;
        this.options = options;
        this.initialValue = initialValue;
        this.currentValue = initialValue;
        this.onChanged = onChanged;
    }

    public VisualElement Build()
    {
        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.justifyContent = Justify.SpaceBetween;

        var lbl = new Label();
        if (labelKey.HasValue)
            LocalizationHelper.LocalizeTextElement(lbl, labelKey.Value);
        else
            lbl.text = label;
        dropdownField = new DropdownField(options, currentValue);
        dropdownField.RegisterValueChangedCallback(evt => {
            currentValue = evt.newValue;
            onChanged?.Invoke(evt.newValue);
            GroupContainerMenuItem.NotifyChange();
        });

        container.Add(lbl);
        container.Add(dropdownField);
        return container;
    }

    public void SetValue(string value)
    {
        currentValue = value;
        if (dropdownField != null)
            dropdownField.value = value;
    }

    public void SetDisplayValue(string value)
    {
        currentValue = value;
        if (dropdownField != null)
            dropdownField.SetValueWithoutNotify(value);
    }

    public void Apply()
    {
        initialValue = currentValue;
        onChanged?.Invoke(currentValue);
    }

    public void Discard()
    {
        currentValue = initialValue;
        if (dropdownField != null)
            dropdownField.value = initialValue;
        onChanged?.Invoke(initialValue);
    }
}

public class SliderSetting : ISettingItem
{
    private readonly UIStringKey? labelKey;
    private readonly string label;
    private readonly float min;
    private readonly float max;
    private float initialValue;
    private float currentValue;
    private readonly System.Action<float> onChanged;

    private Slider slider;

    public SliderSetting(UIStringKey labelKey, float min, float max, float initialValue, System.Action<float> onChanged)
    {
        this.labelKey = labelKey;
        this.label = null;
        this.min = min;
        this.max = max;
        this.initialValue = initialValue;
        this.currentValue = initialValue;
        this.onChanged = onChanged;
    }

    public SliderSetting(string label, float min, float max, float initialValue, System.Action<float> onChanged)
    {
        this.labelKey = null;
        this.label = label;
        this.min = min;
        this.max = max;
        this.initialValue = initialValue;
        this.currentValue = initialValue;
        this.onChanged = onChanged;
    }

    public VisualElement Build()
    {
        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.justifyContent = Justify.SpaceBetween;

        var lbl = new Label();
        if (labelKey.HasValue)
            LocalizationHelper.LocalizeTextElement(lbl, labelKey.Value);
        else
            lbl.text = label;
        slider = new Slider(min, max) { value = initialValue };
        slider.RegisterValueChangedCallback(evt => {
            currentValue = evt.newValue;
            onChanged?.Invoke(evt.newValue);
            GroupContainerMenuItem.NotifyChange();
        });

        container.Add(lbl);
        container.Add(slider);
        return container;
    }

    public void Apply()
    {
        initialValue = currentValue;
        onChanged?.Invoke(currentValue);
    }

    public void Discard()
    {
        currentValue = initialValue;
        if (slider != null)
            slider.value = initialValue;
        onChanged?.Invoke(initialValue);
    }
}

public static class UIUtils
{
    public static VisualElement CreateOrGetLayerColumn(VisualElement currentLayer, int tier)
    {
        var parent = currentLayer.parent;
        int currentIndex = parent.IndexOf(currentLayer);
        int targetIndex = currentIndex + 1;

        // SAFETY: Check if targetIndex makes sense
        if (targetIndex < 0 || targetIndex > parent.childCount)
        {
            Debug.LogError($"Invalid target index: {targetIndex}. Current index: {currentIndex}. Parent has {parent.childCount} children.");
            targetIndex = parent.childCount; // fallback to append at end
        }

        // Remove layers at or beyond targetIndex
        int removedCount = parent.childCount - targetIndex;
        for (int i = parent.childCount - 1; i >= targetIndex; i--)
        {
            var layerToRemove = parent[i];
            layerToRemove.RemoveFromClassList("active");
            layerToRemove.schedule.Execute(() =>
            {
                parent.Remove(layerToRemove);
            }).ExecuteLater(300);
        }

        // Create new layer
        var layer = new VisualElement();
        layer.AddToClassList("menu-column");
        layer.AddToClassList($"tier{tier}-layer");
        layer.style.display = DisplayStyle.None;
        layer.style.justifyContent = Justify.Center;
        parent.Add(layer);

        AdjustColumnFlex(parent);

        // Animate after fade-out completes
        int delay = removedCount > 0 ? 300 : 10;
        layer.schedule.Execute(() => {
            layer.style.display = DisplayStyle.Flex;
            layer.AddToClassList("active");
        }).ExecuteLater(delay);

        return layer;
    }

    public static void AdjustColumnFlex(VisualElement container)
    {
        int count = container.childCount;
        for (int i = 0; i < count; i++)
        {
            var child = container[i];
            child.style.flexGrow = i == count - 1 ? 1f : 0f;
            child.style.flexShrink = 0f;
        }
    }
}

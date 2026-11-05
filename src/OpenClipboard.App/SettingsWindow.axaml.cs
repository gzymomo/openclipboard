using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Clipboard.Data.Enums;
using OpenClipboard.App.Services;

namespace OpenClipboard.App;

public partial class SettingsWindow : Window
{
    private readonly ClipboardPreferences _preferences;
    private readonly LocalizationService _localization;

    public bool Saved { get; private set; }

    public string ActivationHotkey { get; private set; }

    public string QuickCopyHotkey { get; private set; }

    public HistoryMode HistoryMode { get; private set; }

    public double GlassOpacity { get; private set; }

    public System.Collections.Generic.IReadOnlyList<LanguageOption> Languages => _localization.Languages;

    public HistoryModeOption[] HistoryModes { get; }

    public SettingsWindow() : this(new ClipboardPreferences(), new LocalizationService("en-US"))
    {
    }

    public SettingsWindow(ClipboardPreferences preferences, LocalizationService localization)
    {
        _preferences = preferences;
        _localization = localization;
        ActivationHotkey = preferences.ActivationHotkey;
        QuickCopyHotkey = preferences.QuickCopyHotkey;
        HistoryMode = preferences.HistoryMode;
        GlassOpacity = preferences.GlassOpacity;
        HistoryModes =
        [
            new HistoryModeOption(global::Clipboard.Data.Enums.HistoryMode.None, localization.Get("SettingWindow_General_RecordCount_Slider_None_Text")),
            new HistoryModeOption(global::Clipboard.Data.Enums.HistoryMode._7, localization.Get("SettingWindow_General_RecordCount_Slider_7_Text")),
            new HistoryModeOption(global::Clipboard.Data.Enums.HistoryMode._30, localization.Get("SettingWindow_General_RecordCount_Slider_30_Text")),
            new HistoryModeOption(global::Clipboard.Data.Enums.HistoryMode.Forever, localization.Get("SettingWindow_General_RecordCount_Slider_Infinite_Text"))
        ];

        InitializeComponent();
        DataContext = this;
        Title = localization.Get("ClipboardData_MainWindowMenu_Setting");
        LanguageLabel.Text = localization.Get("SettingWindow_General_Language");
        ActivationHotkeyLabel.Text = localization.Get("SettingWindow_General_ActiveHotKey");
        QuickCopyHotkeyLabel.Text = localization.Get("SettingWindow_General_PasteHotKey");
        HistoryModeLabel.Text = localization.Get("SettingWindow_General_RecordCount");
        GlassOpacityLabel.Text = localization.Get("SettingWindow_General_GlassOpacity");
        HotkeyHelpText.Text = localization.Get("CrossPlatform_Hotkey_Help");
        CancelButton.Content = localization.Get("ConfirmWindow_Cancel");
        SaveButton.Content = localization.Get("CrossPlatform_Save");
        LanguageBox.SelectedItem = Languages.FirstOrDefault(language => language.Code == preferences.Language);
        ActivationHotkeyBox.Text = ActivationHotkey;
        QuickCopyHotkeyBox.Text = string.IsNullOrWhiteSpace(QuickCopyHotkey)
            ? localization.Get("CrossPlatform_Disabled")
            : QuickCopyHotkey;
        HistoryModeBox.SelectedItem = HistoryModes.First(option => option.Mode == HistoryMode);
        GlassOpacitySlider.Value = GlassOpacity;
    }

    private void LanguageBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedItem is LanguageOption language)
        {
            _localization.SetLanguage(language.Code);
            RefreshLabels();
        }
    }

    private void RefreshLabels()
    {
        var selectedMode = HistoryModeBox.SelectedItem is HistoryModeOption selected
            ? selected.Mode
            : HistoryMode;
        Title = _localization.Get("ClipboardData_MainWindowMenu_Setting");
        LanguageLabel.Text = _localization.Get("SettingWindow_General_Language");
        ActivationHotkeyLabel.Text = _localization.Get("SettingWindow_General_ActiveHotKey");
        QuickCopyHotkeyLabel.Text = _localization.Get("SettingWindow_General_PasteHotKey");
        HistoryModeLabel.Text = _localization.Get("SettingWindow_General_RecordCount");
        GlassOpacityLabel.Text = _localization.Get("SettingWindow_General_GlassOpacity");
        HotkeyHelpText.Text = _localization.Get("CrossPlatform_Hotkey_Help");
        CancelButton.Content = _localization.Get("ConfirmWindow_Cancel");
        SaveButton.Content = _localization.Get("CrossPlatform_Save");
        HistoryModes[0] = new HistoryModeOption(global::Clipboard.Data.Enums.HistoryMode.None, _localization.Get("SettingWindow_General_RecordCount_Slider_None_Text"));
        HistoryModes[1] = new HistoryModeOption(global::Clipboard.Data.Enums.HistoryMode._7, _localization.Get("SettingWindow_General_RecordCount_Slider_7_Text"));
        HistoryModes[2] = new HistoryModeOption(global::Clipboard.Data.Enums.HistoryMode._30, _localization.Get("SettingWindow_General_RecordCount_Slider_30_Text"));
        HistoryModes[3] = new HistoryModeOption(global::Clipboard.Data.Enums.HistoryMode.Forever, _localization.Get("SettingWindow_General_RecordCount_Slider_Infinite_Text"));
        HistoryModeBox.ItemsSource = HistoryModes;
        HistoryModeBox.SelectedItem = HistoryModes.First(option => option.Mode == selectedMode);
        if (string.IsNullOrWhiteSpace(QuickCopyHotkey))
        {
            QuickCopyHotkeyBox.Text = _localization.Get("CrossPlatform_Disabled");
        }
    }

    private void ActivationHotkeyBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (TryFormatHotkey(e, out var shortcut))
        {
            ActivationHotkey = shortcut;
            ActivationHotkeyBox.Text = shortcut;
            StatusText.Text = string.Empty;
        }
    }

    private void QuickCopyHotkeyBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            QuickCopyHotkey = string.Empty;
            QuickCopyHotkeyBox.Text = _localization.Get("CrossPlatform_Disabled");
            return;
        }

        if (TryFormatHotkey(e, out var shortcut))
        {
            QuickCopyHotkey = shortcut;
            QuickCopyHotkeyBox.Text = shortcut;
            StatusText.Text = string.Empty;
        }
    }

    private bool TryFormatHotkey(KeyEventArgs e, out string shortcut)
    {
        shortcut = string.Empty;
        var key = e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return false;
        }

        var keyValue = (int)key;
        var keyName = keyValue >= (int)Key.A && keyValue <= (int)Key.Z
            || keyValue >= (int)Key.F1 && keyValue <= (int)Key.F24
            ? key.ToString()
            : keyValue >= (int)Key.D0 && keyValue <= (int)Key.D9
                ? (keyValue - (int)Key.D0).ToString()
                : string.Empty;
        if (keyName.Length == 0)
        {
            StatusText.Text = _localization.Get("CrossPlatform_Hotkey_Unsupported");
            return false;
        }

        var modifiers = e.KeyModifiers;
        var parts = new System.Collections.Generic.List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Meta");
        parts.Add(keyName);
        shortcut = string.Join("+", parts);
        return true;
    }

    private void Save_OnClick(object? sender, RoutedEventArgs e)
    {
        if (HistoryModeBox.SelectedItem is not HistoryModeOption selectedMode)
        {
            StatusText.Text = _localization.Get("CrossPlatform_SelectHistoryMode");
            return;
        }

        if (!string.IsNullOrWhiteSpace(QuickCopyHotkey)
            && string.Equals(QuickCopyHotkey, ActivationHotkey, StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = _localization.Get("CrossPlatform_Hotkey_Duplicate");
            return;
        }

        try
        {
            _preferences.Language = (LanguageBox.SelectedItem as LanguageOption)?.Code ?? "en-US";
            _preferences.ActivationHotkey = ActivationHotkey;
            _preferences.QuickCopyHotkey = QuickCopyHotkey;
            _preferences.HistoryMode = selectedMode.Mode;
            _preferences.GlassOpacity = (int)GlassOpacitySlider.Value;
            _preferences.Save();
            Saved = true;
            Close();
        }
        catch (Exception exception)
        {
            StatusText.Text = $"{_localization.Get("CrossPlatform_Save_Failed")}: {exception.Message}";
        }
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close();
}

public sealed record HistoryModeOption(HistoryMode Mode, string DisplayName);

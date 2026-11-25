using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Clipboard.Data.Enums;
using OpenClipboard.App.Services;

namespace OpenClipboard.App;

public partial class SettingsWindow : Window
{
    private readonly ClipboardPreferences _preferences;
    private readonly LocalizationService _localization;
    private readonly StartupManager _startupManager;
    private readonly Func<Window, Task> _clearHistory;
    private readonly Action<bool> _onClosed;
    private bool _autoRunEnabled;

    public bool Saved { get; private set; }

    public string ActivationHotkey { get; private set; }

    public string QuickCopyHotkey { get; private set; }

    public IReadOnlyList<LanguageOption> Languages => _localization.Languages;

    public SettingsWindow() : this(
        new ClipboardPreferences(),
        new LocalizationService("en-US"),
        new StartupManager(),
        _ => Task.CompletedTask)
    {
    }

    public SettingsWindow(
        ClipboardPreferences preferences,
        LocalizationService localization,
        StartupManager? startupManager = null,
        Func<Window, Task>? clearHistory = null,
        Action<bool>? onClosed = null)
    {
        _preferences = preferences;
        _localization = localization;
        _startupManager = startupManager ?? new StartupManager();
        _clearHistory = clearHistory ?? (_ => Task.CompletedTask);
        _onClosed = onClosed ?? (_ => { });
        ActivationHotkey = preferences.ActivationHotkey;
        QuickCopyHotkey = preferences.QuickCopyHotkey;

        InitializeComponent();
        DataContext = this;
        RefreshLabels();
        try
        {
            _autoRunEnabled = _startupManager.IsEnabled();
            AutoRunCheckBox.IsChecked = _autoRunEnabled;
        }
        catch (Exception exception)
        {
            _autoRunEnabled = false;
            AutoRunCheckBox.IsChecked = false;
            HotkeyStatusText.Text = $"Launch-at-login status unavailable: {exception.Message}";
        }
        LanguageBox.SelectedItem = Languages.FirstOrDefault(language => language.Code == preferences.Language);
        ActivationHotkeyBox.Text = ActivationHotkey;
        QuickCopyHotkeyBox.Text = string.IsNullOrWhiteSpace(QuickCopyHotkey)
            ? _localization.Get("CrossPlatform_Disabled")
            : QuickCopyHotkey;
        HistoryModeSlider.Value = (int)preferences.HistoryMode;
        GlassOpacitySlider.Value = preferences.GlassOpacity;
        ScrollWheelSpeedSlider.Value = ScrollSpeedToSlider(preferences.ScrollWheelSpeed);
        ApplyGlassOpacity();
        UpdateHistoryModeText();
        UpdateGlassOpacityText();
        UpdateScrollWheelSpeedText();
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
        Title = _localization.Get("ClipboardData_MainWindowMenu_Setting");
        GeneralTab.Header = _localization.Get("Settings_Tab_General");
        AutoRunLabel.Text = _localization.Get("SettingWindow_General_AutoRun");
        AutoRunCheckBox.Content = _localization.Get("SettingWindow_General_AutoRun_Content");
        LanguageLabel.Text = _localization.Get("SettingWindow_General_Language");
        ActivationHotkeyLabel.Text = _localization.Get("SettingWindow_General_ActiveHotKey");
        QuickCopyHotkeyLabel.Text = _localization.Get("SettingWindow_General_PasteHotKey");
        HistoryModeLabel.Text = _localization.Get("SettingWindow_General_RecordCount");
        GlassOpacityLabel.Text = _localization.Get("SettingWindow_General_GlassOpacity");
        ScrollWheelSpeedLabel.Text = _localization.Get("SettingWindow_General_ScrollWheelSpeed");
        HotkeyHelpText.Text = _localization.Get("CrossPlatform_Hotkey_Help");
        ClearHistoryButton.Content = _localization.Get("SettingWindow_General_ClearHistory");
        CancelButton.Content = _localization.Get("ConfirmWindow_Cancel");
        SaveButton.Content = _localization.Get("CrossPlatform_Save");
        UpdateHistoryModeText();
        UpdateGlassOpacityText();
        UpdateScrollWheelSpeedText();
        if (string.IsNullOrWhiteSpace(QuickCopyHotkey))
        {
            QuickCopyHotkeyBox.Text = _localization.Get("CrossPlatform_Disabled");
        }
    }

    private void AutoRunCheckBox_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var enabled = AutoRunCheckBox.IsChecked == true;
            _startupManager.SetEnabled(enabled);
            _autoRunEnabled = enabled;
            HotkeyStatusText.Text = string.Empty;
        }
        catch (Exception exception)
        {
            HotkeyStatusText.Text = $"Could not update launch-at-login setting: {exception.Message}";
            AutoRunCheckBox.IsChecked = _autoRunEnabled;
        }
    }

    private void ActivationHotkeyBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (TryFormatHotkey(e, out var shortcut))
        {
            ActivationHotkey = shortcut;
            ActivationHotkeyBox.Text = shortcut;
            HotkeyStatusText.Text = string.Empty;
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
            HotkeyStatusText.Text = string.Empty;
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
            HotkeyStatusText.Text = _localization.Get("CrossPlatform_Hotkey_Unsupported");
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

    private void HistoryModeSlider_OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) =>
        UpdateHistoryModeText();

    private void GlassOpacitySlider_OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        UpdateGlassOpacityText();
        ApplyGlassOpacity();
    }

    private void ScrollWheelSpeedSlider_OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) =>
        UpdateScrollWheelSpeedText();

    private void UpdateHistoryModeText()
    {
        if (HistoryModeValueText is null || HistoryModeSlider is null)
        {
            return;
        }

        HistoryModeValueText.Text = _localization.Get((int)Math.Round(HistoryModeSlider.Value) switch
        {
            0 => "SettingWindow_General_RecordCount_Slider_1_Text",
            1 => "SettingWindow_General_RecordCount_Slider_3_Text",
            2 => "SettingWindow_General_RecordCount_Slider_5_Text",
            3 => "SettingWindow_General_RecordCount_Slider_7_Text",
            4 => "SettingWindow_General_RecordCount_Slider_15_Text",
            5 => "SettingWindow_General_RecordCount_Slider_30_Text",
            _ => "SettingWindow_General_RecordCount_Slider_Infinite_Text"
        });
    }

    private void UpdateGlassOpacityText()
    {
        if (GlassOpacityValueText is not null && GlassOpacitySlider is not null)
        {
            GlassOpacityValueText.Text = $"{(int)Math.Round(GlassOpacitySlider.Value)}%";
        }
    }

    private void UpdateScrollWheelSpeedText()
    {
        if (ScrollWheelSpeedValueText is null || ScrollWheelSpeedSlider is null)
        {
            return;
        }

        ScrollWheelSpeedValueText.Text = _localization.Get((int)Math.Round(ScrollWheelSpeedSlider.Value) switch
        {
            <= 0 => "SettingWindow_General_ScrollWheelSpeed_Low",
            1 => "SettingWindow_General_ScrollWheelSpeed_Medium",
            _ => "SettingWindow_General_ScrollWheelSpeed_High"
        });
    }

    private static int ScrollSpeedToSlider(int speed) => speed switch
    {
        <= 70 => 0,
        <= 125 => 1,
        _ => 2
    };

    private static int SliderToScrollSpeed(int sliderValue) => sliderValue switch
    {
        <= 0 => 40,
        1 => 100,
        _ => 150
    };

    private void ApplyGlassOpacity()
    {
        if (MainBackground is null)
        {
            return;
        }

        var opacity = GlassOpacitySlider?.Value ?? _preferences.GlassOpacity;
        var windowAlpha = (byte)Math.Round(opacity * 255 / 100d);
        MainBackground.Background = new SolidColorBrush(Color.FromArgb(windowAlpha, 32, 36, 44));

        // 内容面板沿用窗口的同色系（#20242C 提亮为 #30343C），
        // alpha 随窗口透明度动态变化且始终更高，靠"更实 + 略亮"形成对比，不引入白色。
        var panelAlpha = (byte)Math.Clamp(windowAlpha + 90, 160, 245);
        if (ContentPanelBackground is not null)
        {
            ContentPanelBackground.Background = new SolidColorBrush(Color.FromArgb(panelAlpha, 48, 52, 60));
        }
    }

    private async void ClearHistory_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _clearHistory(this);
            HotkeyStatusText.Text = string.Empty;
        }
        catch (Exception exception)
        {
            HotkeyStatusText.Text = $"Could not clear history: {exception.Message}";
        }
    }

    private void Save_OnClick(object? sender, RoutedEventArgs e)
    {
        var historyMode = (HistoryMode)Math.Clamp((int)Math.Round(HistoryModeSlider.Value), 0, 6);
        if (!string.IsNullOrWhiteSpace(QuickCopyHotkey)
            && string.Equals(QuickCopyHotkey, ActivationHotkey, StringComparison.OrdinalIgnoreCase))
        {
            HotkeyStatusText.Text = _localization.Get("CrossPlatform_Hotkey_Duplicate");
            return;
        }

        try
        {
            _preferences.Language = (LanguageBox.SelectedItem as LanguageOption)?.Code ?? "en-US";
            _preferences.ActivationHotkey = ActivationHotkey;
            _preferences.QuickCopyHotkey = QuickCopyHotkey;
            _preferences.HistoryMode = historyMode;
            _preferences.GlassOpacity = (int)GlassOpacitySlider.Value;
            _preferences.ScrollWheelSpeed = SliderToScrollSpeed((int)Math.Round(ScrollWheelSpeedSlider.Value));
            _preferences.Save();
            Saved = true;
            _onClosed(true);
            Close();
        }
        catch (Exception exception)
        {
            HotkeyStatusText.Text = $"{_localization.Get("CrossPlatform_Save_Failed")}: {exception.Message}";
        }
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!Saved)
        {
            _onClosed(false);
        }
        base.OnClosing(e);
    }
}

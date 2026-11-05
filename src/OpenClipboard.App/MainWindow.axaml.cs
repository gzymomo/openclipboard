using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Clipboard.Data.Enums;
using OpenClipboard.App.Models;
using OpenClipboard.App.Services;

namespace OpenClipboard.App;

public partial class MainWindow : Window
{
    private readonly ClipboardStore _store = new();
    private readonly StartupManager _startupManager = new();
    private readonly GlobalHotkeyService _globalHotkey = new();
    private readonly ClipboardPreferences _preferences;
    private readonly LocalizationService _localization;
    private readonly DispatcherTimer _clipboardTimer;
    private Bitmap? _clipboardBitmap;
    private string? _lastClipboardData;
    private ClipboardDataTypes _lastClipboardType;
    private DateOnly _lastHistoryCleanupDate;
    private bool _isReadingClipboard;
    private bool _allowClose;

    public ObservableCollection<ClipboardHistoryItem> History { get; } = new();

    public ObservableCollection<ClipboardHistoryItem> VisibleHistory { get; } = new();

    public MainWindow() : this(new ClipboardPreferences(), new LocalizationService("en-US"))
    {
    }

    public MainWindow(ClipboardPreferences preferences, LocalizationService localization)
    {
        _preferences = preferences;
        _localization = localization;
        InitializeComponent();
        Title = $"OpenClipboard {App.DisplayVersion}";
        DataContext = this;
        ApplyLocalization();
        ApplyGlassOpacity();
        _clipboardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _clipboardTimer.Tick += ClipboardTimer_Tick;
        Opened += MainWindow_Opened;
        Closed += (_, _) =>
        {
            _clipboardTimer.Stop();
            _globalHotkey.Dispose();
            _clipboardBitmap?.Dispose();
            _store.Dispose();
        };
        Closing += MainWindow_Closing;
        _globalHotkey.ErrorOccurred += message => Dispatcher.UIThread.Post(() =>
            StatusText.Text = $"Global hotkey unavailable: {message}");
        UpdateStartupButton();
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        if (Program.StartedAtLogin)
        {
            Hide();
        }

        try
        {
            await _store.InitializeAsync();
            await _store.PruneExpiredAsync(_preferences.HistoryMode);
            _lastHistoryCleanupDate = DateOnly.FromDateTime(DateTime.UtcNow);
            foreach (var item in await _store.GetRecentAsync(_preferences.HistoryMode))
            {
                History.Add(item);
            }

            if (History.Count > 0)
            {
                _lastClipboardType = History[0].DataType;
                _lastClipboardData = History[0].Data;
            }

            RefreshVisibleHistory();
            _clipboardTimer.Start();
            StartHotkeys();
            StatusText.Text = _localization.Get("CrossPlatform_Monitoring");
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not open clipboard history: {exception.Message}";
        }
    }

    private async void ClipboardTimer_Tick(object? sender, EventArgs e)
    {
        if (_isReadingClipboard)
        {
            return;
        }

        _isReadingClipboard = true;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today != _lastHistoryCleanupDate)
            {
                await _store.PruneExpiredAsync(_preferences.HistoryMode);
                _lastHistoryCleanupDate = today;
                await ReloadHistoryAsync();
            }

            var clipboard = Clipboard ?? throw new InvalidOperationException("The platform clipboard is unavailable.");
            using var data = await clipboard.TryGetDataAsync();
            if (data is null)
            {
                return;
            }

            var item = await CaptureClipboardItemAsync(data);
            if (item is null || item.Data.Length == 0
                || (item.DataType == _lastClipboardType && string.Equals(item.Data, _lastClipboardData, StringComparison.Ordinal)))
            {
                return;
            }

            if (_preferences.HistoryMode == HistoryMode.None)
            {
                return;
            }

            await _store.AddAsync(item);
            _lastClipboardType = item.DataType;
            _lastClipboardData = item.Data;
            History.Insert(0, item);
            RefreshVisibleHistory();
            StatusText.Text = $"Saved {item.DataType.ToString().ToLowerInvariant()} clipboard content.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not update clipboard history: {exception.Message}";
        }
        finally
        {
            _isReadingClipboard = false;
        }
    }

    private async Task<ClipboardHistoryItem?> CaptureClipboardItemAsync(IAsyncDataTransfer data)
    {
        var text = await data.TryGetTextAsync();
        if (!string.IsNullOrEmpty(text))
        {
            return new ClipboardHistoryItem { DataType = ClipboardDataTypes.Text, Data = text };
        }

        using var bitmap = await data.TryGetBitmapAsync();
        if (bitmap is not null)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, new PngBitmapEncoderOptions());
            return new ClipboardHistoryItem
            {
                DataType = ClipboardDataTypes.Image,
                Data = Convert.ToBase64String(stream.ToArray())
            };
        }

        var files = await data.TryGetFilesAsync() ?? Array.Empty<IStorageItem>();
        var paths = files
            .Select(file => file.Path.IsFile ? file.Path.LocalPath : file.Path.ToString())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        return paths.Length == 0
            ? null
            : new ClipboardHistoryItem
            {
                DataType = ClipboardDataTypes.File,
                Data = JsonSerializer.Serialize(paths)
            };
    }

    private void HistoryList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var hasSelection = HistoryList.SelectedItem is ClipboardHistoryItem;
        CopyButton.IsEnabled = hasSelection;
        FavoriteButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
        FavoriteButton.Content = _localization.Get(HistoryList.SelectedItem is ClipboardHistoryItem { IsFavorite: true }
            ? "CrossPlatform_Unfavorite"
            : "CrossPlatform_Favorite");
    }

    private async void Copy_OnClick(object? sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not ClipboardHistoryItem item)
        {
            return;
        }

        try
        {
            var clipboard = Clipboard ?? throw new InvalidOperationException("The platform clipboard is unavailable.");
            switch (item.DataType)
            {
                case ClipboardDataTypes.Text:
                    var textTransfer = new DataTransfer();
                    textTransfer.Add(DataTransferItem.CreateText(item.Data));
                    await clipboard.SetDataAsync(textTransfer);
                    await clipboard.FlushAsync();
                    _clipboardBitmap?.Dispose();
                    _clipboardBitmap = null;
                    break;
                case ClipboardDataTypes.Image:
                    using (var stream = new MemoryStream(Convert.FromBase64String(item.Data)))
                    {
                        Bitmap? bitmap = new Bitmap(stream);
                        try
                        {
                            var imageTransfer = new DataTransfer();
                            imageTransfer.Add(DataTransferItem.Create(DataFormat.Bitmap, bitmap));
                            await clipboard.SetDataAsync(imageTransfer);
                            var previousBitmap = _clipboardBitmap;
                            _clipboardBitmap = bitmap;
                            bitmap = null;
                            previousBitmap?.Dispose();
                            await clipboard.FlushAsync();
                        }
                        finally
                        {
                            bitmap?.Dispose();
                        }
                    }
                    break;
                case ClipboardDataTypes.File:
                    var paths = JsonSerializer.Deserialize<string[]>(item.Data) ?? Array.Empty<string>();
                    var transfer = new DataTransfer();
                    foreach (var path in paths)
                    {
                        var uri = new Uri(Path.GetFullPath(path));
                        var file = await StorageProvider.TryGetFileFromPathAsync(uri);
                        var folder = file is null ? await StorageProvider.TryGetFolderFromPathAsync(uri) : null;
                        var storageItem = (IStorageItem?)file ?? folder;
                        if (storageItem is null)
                        {
                            throw new FileNotFoundException("A clipboard file no longer exists.", path);
                        }

                        transfer.Add(DataTransferItem.CreateFile(storageItem));
                    }

                    await clipboard.SetDataAsync(transfer);
                    await clipboard.FlushAsync();
                    _clipboardBitmap?.Dispose();
                    _clipboardBitmap = null;
                    break;
                default:
                    throw new NotSupportedException($"Clipboard data type '{item.DataType}' cannot be copied.");
            }

            _lastClipboardType = item.DataType;
            _lastClipboardData = item.Data;
            StatusText.Text = $"Copied {item.DataType.ToString().ToLowerInvariant()} content.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not copy clipboard content: {exception.Message}";
        }
    }

    private async void Favorite_OnClick(object? sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not ClipboardHistoryItem item)
        {
            return;
        }

        try
        {
            var updated = item with { IsFavorite = !item.IsFavorite };
            await _store.SetFavoriteAsync(item.Id, updated.IsFavorite);
            ReplaceItem(item, updated);
            HistoryList.SelectedItem = updated;
            StatusText.Text = updated.IsFavorite ? "Added to favorites." : "Removed from favorites.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not update favorite: {exception.Message}";
        }
    }

    private async void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not ClipboardHistoryItem item)
        {
            return;
        }

        try
        {
            await _store.DeleteAsync(item.Id);
            History.Remove(item);
            RefreshVisibleHistory();
            StatusText.Text = "History entry deleted.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not delete history entry: {exception.Message}";
        }
    }

    private async void Clear_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _store.ClearAsync();
            History.Clear();
            RefreshVisibleHistory();
            StatusText.Text = "Clipboard history cleared.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not clear history: {exception.Message}";
        }
    }

    private async void ImportLegacy_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var selected = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import an OpenClipboard database",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("SQLite database") { Patterns = new[] { "*.db", "*.sqlite", "*.sqlite3" } }
                }
            });
            if (selected.Count == 0)
            {
                return;
            }

            var imported = await _store.ImportLegacyDatabaseAsync(selected[0].Path.LocalPath);
            History.Clear();
            foreach (var item in await _store.GetRecentAsync(_preferences.HistoryMode))
            {
                History.Add(item);
            }

            RefreshVisibleHistory();
            StatusText.Text = $"Imported {imported} history entr{(imported == 1 ? "y" : "ies")}.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not import database: {exception.Message}";
        }
    }

    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e) => RefreshVisibleHistory();

    private async void Settings_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_preferences, _localization);
        await dialog.ShowDialog(this);
        if (!dialog.Saved)
        {
            _localization.SetLanguage(_preferences.Language);
            ApplyLocalization();
            return;
        }

        try
        {
            _localization.SetLanguage(_preferences.Language);
            ApplyLocalization();
            ApplyGlassOpacity();
            await _store.PruneExpiredAsync(_preferences.HistoryMode);
            _lastHistoryCleanupDate = DateOnly.FromDateTime(DateTime.UtcNow);
            await ReloadHistoryAsync();
            StartHotkeys();
            StatusText.Text = _localization.Get("CrossPlatform_Monitoring");
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not apply preferences: {exception.Message}";
        }
    }

    private void StartHotkeys()
    {
        _globalHotkey.Start(
            _preferences.ActivationHotkey,
            _preferences.QuickCopyHotkey,
            () => Dispatcher.UIThread.Post(ToggleFromHotkey),
            () => Dispatcher.UIThread.Post(CopyLatest));
    }

    private async Task ReloadHistoryAsync()
    {
        History.Clear();
        foreach (var item in await _store.GetRecentAsync(_preferences.HistoryMode))
        {
            History.Add(item);
        }

        RefreshVisibleHistory();
    }

    private void CopyLatest()
    {
        if (History.Count == 0)
        {
            return;
        }

        HistoryList.SelectedItem = History[0];
        Copy_OnClick(CopyButton, new RoutedEventArgs());
    }

    private void ApplyLocalization()
    {
        SubtitleText.Text = _localization.Get("ClipboardDataTag_History");
        SearchBox.PlaceholderText = _localization.Get("ClipboardDataSearchText_PlaceHolder");
        ImportButton.Content = _localization.Get("CrossPlatform_ImportLegacy");
        SettingsButton.Content = _localization.Get("ClipboardData_MainWindowMenu_Setting");
        CopyButton.Content = _localization.Get("History_Listbox_Copy");
        FavoriteButton.Content = _localization.Get("History_Listbox_Favorite");
        DeleteButton.Content = _localization.Get("History_Listbox_Delete");
        ClearButton.Content = _localization.Get("History_RadioButton_ClearHistory");
        QuitButton.Content = _localization.Get("ClipboardData_MainWindowMenu_Exit");
        UpdateStartupButton();
        (Avalonia.Application.Current as App)?.ApplyLocalization();
    }

    private void ApplyGlassOpacity()
    {
        var alpha = (byte)Math.Round(_preferences.GlassOpacity * 255 / 100d);
        MainBackground.Background = new SolidColorBrush(Color.FromArgb(alpha, 32, 36, 44));
    }

    private void RefreshVisibleHistory()
    {
        var query = SearchBox?.Text?.Trim();
        var matches = string.IsNullOrEmpty(query)
            ? History
            : new ObservableCollection<ClipboardHistoryItem>(History.Where(item =>
                item.Preview.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        VisibleHistory.Clear();
        foreach (var item in matches)
        {
            VisibleHistory.Add(item);
        }
    }

    private void ReplaceItem(ClipboardHistoryItem original, ClipboardHistoryItem updated)
    {
        var index = History.IndexOf(original);
        if (index >= 0)
        {
            History[index] = updated;
            RefreshVisibleHistory();
        }
    }

    private void Startup_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            _startupManager.SetEnabled(!_startupManager.IsEnabled());
            UpdateStartupButton();
            StatusText.Text = _startupManager.IsEnabled() ? "Launch at login enabled." : "Launch at login disabled.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not update launch-at-login setting: {exception.Message}";
        }
    }

    private void UpdateStartupButton()
    {
        try
        {
            StartupButton.Content = _localization.Get(_startupManager.IsEnabled()
                ? "CrossPlatform_StartupDisable"
                : "CrossPlatform_StartupEnable");
        }
        catch (Exception exception)
        {
            StartupButton.IsEnabled = false;
            StatusText.Text = $"Launch-at-login status unavailable: {exception.Message}";
        }
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ToggleFromHotkey()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            ShowFromTray();
        }
    }

    public void AllowClose() => _allowClose = true;

    private void Quit_OnClick(object? sender, RoutedEventArgs e)
    {
        AllowClose();
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
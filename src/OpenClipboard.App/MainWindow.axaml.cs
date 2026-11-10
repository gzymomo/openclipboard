using System;
using System.Collections.ObjectModel;
using System.Globalization;
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
    private bool _showFavorites;
    private bool _allowClose;

    public ObservableCollection<ClipboardHistoryItem> History { get; } = new();

    public ObservableCollection<ClipboardHistoryItem> VisibleHistory { get; } = new();

    public MainWindow() : this(new ClipboardPreferences(), new LocalizationService("en-US"))
    {
    }

    private void MainWindow_OnOpened(object? sender, EventArgs e)
    {
        var screen = Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var bounds = screen.Bounds;
        Width = Math.Max(MinWidth, bounds.Width / screen.Scaling);
        Height = 400;
        Position = new Avalonia.PixelPoint(
            bounds.X,
            bounds.Y + bounds.Height - (int)Math.Round(Height * screen.Scaling));
        Topmost = true;
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
        Opened += MainWindow_OnOpened;
        Opened += MainWindow_Opened;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        };
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
                History.Add(LocalizeItem(item));
            }

            if (History.Count > 0)
            {
                _lastClipboardType = History[0].DataType;
                _lastClipboardData = History[0].Data;
            }

            RefreshVisibleHistory();
            _clipboardTimer.Start();
            StartHotkeys();
            StatusText.Text = string.Empty;
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
            History.Insert(0, LocalizeItem(item));
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
        var selected = HistoryList.SelectedItems?.Cast<ClipboardHistoryItem>().ToArray() ?? Array.Empty<ClipboardHistoryItem>();
        CopyMenuItem.IsEnabled = selected.Length > 0;
        FavoriteMenuItem.IsEnabled = selected.Length > 0;
        DeleteMenuItem.IsEnabled = selected.Length > 0;
        FavoriteMenuItem.Header = _localization.Get(selected.Length > 0 && selected.All(item => item.IsFavorite)
            ? "CrossPlatform_Unfavorite"
            : "CrossPlatform_Favorite");
    }

    private async void Copy_OnClick(object? sender, RoutedEventArgs e)
    {
        var selected = HistoryList.SelectedItems?.Cast<ClipboardHistoryItem>().ToArray() ?? Array.Empty<ClipboardHistoryItem>();
        if (selected.Length == 0 && HistoryList.SelectedItem is ClipboardHistoryItem single)
        {
            selected = new[] { single };
        }

        if (selected.Length == 0)
        {
            return;
        }

        try
        {
            var textItems = selected.Where(item => item.DataType == ClipboardDataTypes.Text).ToArray();
            if (textItems.Length > 0)
            {
                await CopyItemAsync(new ClipboardHistoryItem
                {
                    DataType = ClipboardDataTypes.Text,
                    Data = string.Join(Environment.NewLine, textItems.Select(item => item.Data))
                });
            }
            else if (selected[0].DataType == ClipboardDataTypes.Image)
            {
                await CopyItemAsync(selected[0]);
            }
            else
            {
                var paths = selected
                    .Where(item => item.DataType == ClipboardDataTypes.File)
                    .SelectMany(item => JsonSerializer.Deserialize<string[]>(item.Data) ?? Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                await CopyItemAsync(new ClipboardHistoryItem
                {
                    DataType = ClipboardDataTypes.File,
                    Data = JsonSerializer.Serialize(paths)
                });
            }
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not copy clipboard content: {exception.Message}";
        }
    }

    private async Task<bool> CopyItemAsync(ClipboardHistoryItem item)
    {
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
            return true;
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not copy clipboard content: {exception.Message}";
            return false;
        }
    }

    private async void Favorite_OnClick(object? sender, RoutedEventArgs e)
    {
        var selected = HistoryList.SelectedItems?.Cast<ClipboardHistoryItem>().ToArray() ?? Array.Empty<ClipboardHistoryItem>();
        if (selected.Length == 0 && HistoryList.SelectedItem is ClipboardHistoryItem single)
        {
            selected = new[] { single };
        }

        var makeFavorite = !selected.All(item => item.IsFavorite);
        foreach (var item in selected)
        {
            try
            {
                await _store.SetFavoriteAsync(item.Id, makeFavorite);
                ReplaceItem(item, LocalizeItem(item with { IsFavorite = makeFavorite }));
            }
            catch (Exception exception)
            {
                StatusText.Text = $"Could not update favorite: {exception.Message}";
                return;
            }
        }

        StatusText.Text = makeFavorite ? "Added to favorites." : "Removed from favorites.";
    }

    private async void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        var selected = HistoryList.SelectedItems?.Cast<ClipboardHistoryItem>().ToArray() ?? Array.Empty<ClipboardHistoryItem>();
        if (selected.Length == 0 && HistoryList.SelectedItem is ClipboardHistoryItem single)
        {
            selected = new[] { single };
        }

        foreach (var item in selected)
        {
            try
            {
                await _store.DeleteAsync(item.Id);
                History.Remove(item);
            }
            catch (Exception exception)
            {
                StatusText.Text = $"Could not delete history entry: {exception.Message}";
                return;
            }
        }

        RefreshVisibleHistory();
        StatusText.Text = "History entry deleted.";
    }

    private async void Clear_OnClick(object? sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        if (!await ConfirmClearAsync())
        {
            return;
        }

        try
        {
            await ClearHistoryAsync();
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not clear history: {exception.Message}";
        }
    }

    private async Task ClearHistoryFromSettingsAsync(Window owner)
    {
        if (await ConfirmClearAsync(owner))
        {
            await ClearHistoryAsync();
        }
    }

    private async Task ClearHistoryAsync()
    {
        await _store.ClearAsync();
        History.Clear();
        RefreshVisibleHistory();
        StatusText.Text = "Clipboard history cleared.";
    }

    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e) => RefreshVisibleHistory();

    private void SearchToggle_OnClick(object? sender, RoutedEventArgs e)
    {
        SearchPanel.IsVisible = !SearchPanel.IsVisible;
        if (SearchPanel.IsVisible)
        {
            SearchBox.Focus();
        }
        else
        {
            SearchBox.Text = string.Empty;
        }
    }

    private void SearchBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            SearchPanel.IsVisible = false;
        }
    }

    private void SearchClear_OnClick(object? sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        SearchBox.Focus();
    }

    private void HistoryFilter_OnClick(object? sender, RoutedEventArgs e)
    {
        _showFavorites = false;
        RefreshFilterButtons();
        RefreshVisibleHistory();
    }

    private void FavoritesFilter_OnClick(object? sender, RoutedEventArgs e)
    {
        _showFavorites = true;
        RefreshFilterButtons();
        RefreshVisibleHistory();
    }

    private void RefreshFilterButtons()
    {
        HistoryFilterButton.Opacity = _showFavorites ? 0.65 : 1;
        FavoritesFilterButton.Opacity = _showFavorites ? 1 : 0.65;
    }

    private void Menu_OnClick(object? sender, RoutedEventArgs e) => MenuPopup.IsOpen = !MenuPopup.IsOpen;

    private async void HistoryItem_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: ClipboardHistoryItem item })
        {
            HistoryList.SelectedItem = item;
            if (await CopyItemAsync(item))
            {
                Hide();
            }
        }
    }

    private void HistoryList_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        HistoryScroller.Offset = new Avalonia.Vector(
            Math.Max(0, HistoryScroller.Offset.X - e.Delta.Y * 40),
            HistoryScroller.Offset.Y);
        e.Handled = true;
    }

    private async Task<bool> ConfirmClearAsync(Window? owner = null)
    {
        var dialog = new Window
        {
            Title = _localization.Get("ConfirmWindow_Title_Text"),
            Width = 360,
            Height = 150,
            MinWidth = 360,
            MinHeight = 150,
            MaxWidth = 360,
            MaxHeight = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        var confirmButton = new Button { Content = _localization.Get("ConfirmWindow_OK") };
        var cancelButton = new Button { Content = _localization.Get("ConfirmWindow_Cancel") };
        confirmButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = _localization.Get("ConfirmWindow_Content_Text"), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, confirmButton }
                }
            }
        };
        return await dialog.ShowDialog<bool>(owner ?? this);
    }

    private async void Settings_OnClick(object? sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        var dialog = new SettingsWindow(_preferences, _localization, _startupManager, ClearHistoryFromSettingsAsync);
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
            StatusText.Text = string.Empty;
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
            History.Add(LocalizeItem(item));
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
        Copy_OnClick(null, new RoutedEventArgs());
    }

    private void ApplyLocalization()
    {
        SearchBox.PlaceholderText = _localization.Get("ClipboardDataSearchText_PlaceHolder");
        HistoryFilterButton.Content = _localization.Get("ClipboardDataTag_History");
        FavoritesFilterButton.Content = _localization.Get("ClipboardDataTag_Favorite");
        SettingsButton.Content = _localization.Get("ClipboardData_MainWindowMenu_Setting");
        CopyMenuItem.Header = _localization.Get("History_Listbox_Copy");
        FavoriteMenuItem.Header = _localization.Get("CrossPlatform_Favorite");
        DeleteMenuItem.Header = _localization.Get("History_Listbox_Delete");
        ClearButton.Content = _localization.Get("History_RadioButton_ClearHistory");
        QuitButton.Content = _localization.Get("ClipboardData_MainWindowMenu_Exit");
        UpdateStartupButton();
        RefreshFilterButtons();
        for (var index = 0; index < History.Count; index++)
        {
            History[index] = LocalizeItem(History[index]);
        }

        RefreshVisibleHistory();
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
                item.Data.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        VisibleHistory.Clear();
        foreach (var item in matches.Where(item => !_showFavorites || item.IsFavorite))
        {
            VisibleHistory.Add(item);
        }

        EmptyHistoryText.Text = _localization.Get("ClipboardDataTag_History_Empty");
        EmptyHistoryText.IsVisible = VisibleHistory.Count == 0;
    }

    private ClipboardHistoryItem LocalizeItem(ClipboardHistoryItem item)
    {
        var typeLabel = item.DataType == ClipboardDataTypes.File
            ? string.Format(CultureInfo.CurrentCulture, _localization.Get("ClipboardDataType_File"), GetFileCount(item.Data))
            : _localization.Get($"ClipboardDataType_{item.DataType}");
        var itemInfo = item.DataType switch
        {
            ClipboardDataTypes.Text => string.Format(
                CultureInfo.CurrentCulture,
                _localization.Get("ClipboardDataType_Item_Info_Text"),
                item.Data.Length),
            ClipboardDataTypes.Image => _localization.Get("ClipboardDataType_Image"),
            ClipboardDataTypes.File => typeLabel,
            _ => string.Empty
        };
        var preview = item.DataType == ClipboardDataTypes.Image
            ? _localization.Get("ClipboardDataType_Image")
            : item.Preview;
        var image = item.DataType == ClipboardDataTypes.Image ? TryCreateBitmap(item.Data) : item.ImageBitmap;
        return item with { TypeLabel = typeLabel, ItemInfo = itemInfo, DisplayPreview = preview, ImageBitmap = image };
    }

    private static Bitmap? TryCreateBitmap(string data)
    {
        try
        {
            var bytes = Convert.FromBase64String(data);
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private static int GetFileCount(string data)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(data)?.Length ?? 0;
        }
        catch (JsonException)
        {
            return 0;
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
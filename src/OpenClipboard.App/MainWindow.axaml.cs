using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
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
using OpenClipboard.App.Services.Source;

namespace OpenClipboard.App;

public partial class MainWindow : Window
{
    private readonly ClipboardStore _store = new();
    private readonly StartupManager _startupManager = new();
    private readonly GlobalHotkeyService _globalHotkey = new();
    private readonly ISourceAppProvider _sourceAppProvider = new SourceAppProvider();
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
    private DateTimeOffset _suppressDeactivateHideUntil;
    private bool _isLoadingMoreHistory;
    private bool _hasMoreHistory = true;
    private DateTime? _historyCursorUtc;
    private string? _historyCursorId;
    private CancellationTokenSource? _searchCts;
    private int _historyLoadVersion;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Bitmap> _sourceIconCache = new(StringComparer.OrdinalIgnoreCase);
    private const int HistoryPageSize = 50;

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
    }

    private bool _initialized;

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
        HistoryScroller.ScrollChanged += HistoryScroller_OnScrollChanged;
        Opened += MainWindow_OnOpened;
        Opened += async (_, _) => await EnsureInitializedAsync();
        Deactivated += (_, _) => Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(120);
            if (!IsVisible || _allowClose)
            {
                return;
            }

            if (OwnedWindows.Count > 0)
            {
                return;
            }

            if (DateTimeOffset.UtcNow < _suppressDeactivateHideUntil)
            {
                return;
            }

            if (!IsActive)
            {
                Hide();
            }
        });
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

        // Ensure history loading and global hotkeys are started even if the window is never shown
        // (tray-first startup). Opened will also call this once the window becomes visible.
        _ = Dispatcher.UIThread.InvokeAsync(async () => await EnsureInitializedAsync());
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        if (IsVisible && Program.StartedAtLogin)
        {
            Hide();
        }

        try
        {
            await _store.InitializeAsync();
            await _store.PruneExpiredAsync(_preferences.HistoryMode);
            _lastHistoryCleanupDate = DateOnly.FromDateTime(DateTime.UtcNow);
            await ReloadHistoryAsync();

            if (History.Count > 0)
            {
                _lastClipboardType = History[0].DataType;
                _lastClipboardData = History[0].Data;
            }

            _clipboardTimer.Start();
            StartHotkeys();
            StatusText.Text = string.Empty;
        }
        catch (Exception exception)
        {
            _initialized = false;
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

            // History is always recorded; retention is controlled by HistoryMode.

            var source = await _sourceAppProvider.GetCurrentSourceAsync();
            if (source is not null)
            {
                item = item with
                {
                    SourceAppName = source.AppName,
                    SourceAppId = source.AppId,
                    SourceIconPath = source.IconPath
                };
            }

            await _store.AddAsync(item);
            _lastClipboardType = item.DataType;
            _lastClipboardData = item.Data;
            var localized = LocalizeItem(item);
            if (MatchesCurrentFilter(localized))
            {
                History.Insert(0, localized);
                VisibleHistory.Insert(0, localized);
                EmptyHistoryText.IsVisible = VisibleHistory.Count == 0;
                QueueBitmapHydration(new[] { localized });
            }
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
            return new ClipboardHistoryItem { DataType = DetectTextDataType(text), Data = text };
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
            var textItems = selected.Where(IsTextLikeDataType).ToArray();
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
                case ClipboardDataTypes.Color:
                case ClipboardDataTypes.Url:
                case ClipboardDataTypes.Json:
                case ClipboardDataTypes.Xml:
                case ClipboardDataTypes.Markdown:
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
        ResetHistoryPaging();
        _hasMoreHistory = false;
        _historyLoadVersion++;
        History.Clear();
        RefreshVisibleHistory();
        StatusText.Text = "Clipboard history cleared.";
    }

    private async void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _searchCts, cts);
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await Task.Delay(250, cts.Token);
            await ReloadHistoryAsync();
        }
        catch (OperationCanceledException)
        {
            // Debounced by a newer search change.
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not search history: {exception.Message}";
        }
    }

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

    private async void HistoryFilter_OnClick(object? sender, RoutedEventArgs e)
    {
        _showFavorites = false;
        RefreshFilterButtons();
        await ReloadHistoryAsync();
    }

    private async void FavoritesFilter_OnClick(object? sender, RoutedEventArgs e)
    {
        _showFavorites = true;
        RefreshFilterButtons();
        await ReloadHistoryAsync();
    }

    private void RefreshFilterButtons()
    {
        HistoryFilterButton.Opacity = _showFavorites ? 0.65 : 1;
        FavoritesFilterButton.Opacity = _showFavorites ? 1 : 0.65;
    }

    private void Menu_OnClick(object? sender, RoutedEventArgs e) => MenuPopup.IsOpen = !MenuPopup.IsOpen;

    private void Latest_OnClick(object? sender, RoutedEventArgs e)
    {
        HistoryScroller.Offset = new Avalonia.Vector(0, HistoryScroller.Offset.Y);
        if (VisibleHistory.Count > 0)
        {
            HistoryList.SelectedItem = VisibleHistory[0];
            HistoryList.ScrollIntoView(VisibleHistory[0]);
        }
    }

    private async void HistoryItem_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: ClipboardHistoryItem item })
        {
            HistoryList.SelectedItem = item;
            if (await CopyItemAsync(item))
            {
                ShowCopySuccessOverlay();
                Hide();
            }
        }
    }

    private void ShowCopySuccessOverlay()
    {
        try
        {
            var overlay = new CopySuccessOverlay(_localization.Get("CopySuccessWindow_CopySuccess"));
            overlay.Show();
        }
        catch
        {
            // Overlay is best-effort; copy already succeeded.
        }
    }

    private void HistoryList_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        HistoryScroller.Offset = new Avalonia.Vector(
            Math.Max(0, HistoryScroller.Offset.X - e.Delta.Y * _preferences.ScrollWheelSpeed),
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
            CanResize = false,
            ShowInTaskbar = false
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
        var dialogOwner = owner ?? this;
        dialog.Opened += (_, _) =>
        {
            dialog.Topmost = true;
            dialog.Activate();
            dialog.Topmost = false;
        };
        return await dialog.ShowDialog<bool>(dialogOwner);
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

    private (string? searchText, bool favoritesOnly) GetHistoryQueryFilter()
    {
        var search = SearchBox?.Text?.Trim();
        return (string.IsNullOrWhiteSpace(search) ? null : search, _showFavorites);
    }

    private bool MatchesCurrentFilter(ClipboardHistoryItem item)
    {
        var (searchText, favoritesOnly) = GetHistoryQueryFilter();
        if (favoritesOnly && !item.IsFavorite)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(searchText)
            && !item.Data.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private void ResetHistoryPaging()
    {
        _historyCursorUtc = null;
        _historyCursorId = null;
        _hasMoreHistory = true;
    }

    private async Task ReloadHistoryAsync()
    {
        var version = ++_historyLoadVersion;
        var (searchText, favoritesOnly) = GetHistoryQueryFilter();
        var items = await _store.GetRecentAsync(_preferences.HistoryMode, HistoryPageSize, null, null, searchText, favoritesOnly);
        if (version != _historyLoadVersion)
        {
            return;
        }

        ResetHistoryPaging();
        History.Clear();

        var loaded = new List<ClipboardHistoryItem>(items.Count);
        foreach (var item in items)
        {
            loaded.Add(LocalizeItem(item));
        }

        if (version != _historyLoadVersion)
        {
            return;
        }

        foreach (var item in loaded)
        {
            History.Add(item);
        }

        if (items.Count > 0)
        {
            var last = items[^1];
            _historyCursorUtc = last.CreateTime.ToUniversalTime();
            _historyCursorId = last.Id;
        }
        _hasMoreHistory = items.Count >= HistoryPageSize;

        AppendVisibleItems(loaded, reset: true);
        QueueBitmapHydration(loaded);
    }

    private async Task LoadMoreHistoryAsync()
    {
        if (_isLoadingMoreHistory || !_hasMoreHistory)
        {
            return;
        }

        var version = _historyLoadVersion;
        var (searchText, favoritesOnly) = GetHistoryQueryFilter();
        var cursorUtc = _historyCursorUtc;
        var cursorId = _historyCursorId;
        _isLoadingMoreHistory = true;
        try
        {
            var items = await _store.GetRecentAsync(_preferences.HistoryMode, HistoryPageSize, cursorUtc, cursorId, searchText, favoritesOnly);
            if (version != _historyLoadVersion)
            {
                return;
            }

            if (items.Count == 0)
            {
                _hasMoreHistory = false;
                return;
            }

            var existingIds = History.Select(h => h.Id).ToHashSet(StringComparer.Ordinal);
            var added = new List<ClipboardHistoryItem>();
            foreach (var item in items)
            {
                if (existingIds.Add(item.Id))
                {
                    var localized = LocalizeItem(item);
                    History.Add(localized);
                    added.Add(localized);
                }
            }

            var last = items[^1];
            _historyCursorUtc = last.CreateTime.ToUniversalTime();
            _historyCursorId = last.Id;
            _hasMoreHistory = items.Count >= HistoryPageSize;

            if (added.Count > 0)
            {
                AppendVisibleItems(added, reset: false);
                QueueBitmapHydration(added);
            }
        }
        finally
        {
            _isLoadingMoreHistory = false;
        }
    }

    private void HistoryScroller_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!_hasMoreHistory || _isLoadingMoreHistory)
        {
            return;
        }

        var scrollViewer = HistoryScroller;
        var remaining = scrollViewer.Extent.Width - (scrollViewer.Offset.X + scrollViewer.Viewport.Width);
        if (remaining <= Math.Max(scrollViewer.Viewport.Width, 300))
        {
            _ = LoadMoreHistoryAsync();
        }
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
        ToolTip.SetTip(LatestButton, _localization.Get("CrossPlatform_JumpToLatest"));
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
        var tint = new SolidColorBrush(Color.FromArgb(alpha, 32, 36, 44));
        MainBackground.Background = tint;
        if (MenuPopupBorder is not null)
        {
            MenuPopupBorder.Background = tint;
        }
    }

    private void RefreshVisibleHistory()
    {
        VisibleHistory.Clear();
        foreach (var item in History)
        {
            VisibleHistory.Add(item);
        }

        EmptyHistoryText.Text = _localization.Get("ClipboardDataTag_History_Empty");
        EmptyHistoryText.IsVisible = VisibleHistory.Count == 0;
    }

    private void AppendVisibleItems(IReadOnlyList<ClipboardHistoryItem> items, bool reset)
    {
        if (reset)
        {
            VisibleHistory.Clear();
        }

        foreach (var item in items)
        {
            VisibleHistory.Add(item);
        }

        EmptyHistoryText.Text = _localization.Get("ClipboardDataTag_History_Empty");
        EmptyHistoryText.IsVisible = VisibleHistory.Count == 0;
    }

    private void QueueBitmapHydration(IReadOnlyList<ClipboardHistoryItem> items)
    {
        foreach (var item in items)
        {
            if (item.DataType == ClipboardDataTypes.Image && item.ImageBitmap is null)
            {
                _ = HydrateImageAsync(item);
            }

            if (!string.IsNullOrWhiteSpace(item.SourceIconPath)
                && item.SourceIconBitmap is null
                && !_sourceIconCache.ContainsKey(item.SourceIconPath))
            {
                _ = HydrateSourceIconAsync(item.SourceIconPath!);
            }
        }
    }

    private async Task HydrateImageAsync(ClipboardHistoryItem item)
    {
        try
        {
            var bytes = await Task.Run(() => Convert.FromBase64String(item.Data));
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                using var stream = new MemoryStream(bytes);
                var bitmap = new Bitmap(stream);
                ReplaceHistoryBitmap(item.Id, image: bitmap);
            });
        }
        catch
        {
            // Ignore invalid image payloads.
        }
    }

    private async Task HydrateSourceIconAsync(string iconPath)
    {
        try
        {
            var bytes = await Task.Run(() => File.Exists(iconPath) ? File.ReadAllBytes(iconPath) : null);
            if (bytes is null)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_sourceIconCache.ContainsKey(iconPath))
                {
                    ApplySourceIconToHistory(iconPath, _sourceIconCache[iconPath]);
                    return;
                }

                using var stream = new MemoryStream(bytes);
                var bitmap = new Bitmap(stream);
                if (_sourceIconCache.TryAdd(iconPath, bitmap))
                {
                    ApplySourceIconToHistory(iconPath, bitmap);
                }
                else
                {
                    bitmap.Dispose();
                }
            });
        }
        catch
        {
            // Ignore unreadable icon files.
        }
    }

    private void ReplaceHistoryBitmap(string id, Bitmap image)
    {
        for (var i = 0; i < History.Count; i++)
        {
            if (History[i].Id == id && History[i].ImageBitmap is null)
            {
                History[i] = History[i] with { ImageBitmap = image };
            }
        }

        for (var i = 0; i < VisibleHistory.Count; i++)
        {
            if (VisibleHistory[i].Id == id && VisibleHistory[i].ImageBitmap is null)
            {
                VisibleHistory[i] = VisibleHistory[i] with { ImageBitmap = image };
            }
        }
    }

    private void ApplySourceIconToHistory(string iconPath, Bitmap icon)
    {
        for (var i = 0; i < History.Count; i++)
        {
            if (string.Equals(History[i].SourceIconPath, iconPath, StringComparison.OrdinalIgnoreCase)
                && History[i].SourceIconBitmap is null)
            {
                History[i] = History[i] with { SourceIconBitmap = icon };
            }
        }

        for (var i = 0; i < VisibleHistory.Count; i++)
        {
            if (string.Equals(VisibleHistory[i].SourceIconPath, iconPath, StringComparison.OrdinalIgnoreCase)
                && VisibleHistory[i].SourceIconBitmap is null)
            {
                VisibleHistory[i] = VisibleHistory[i] with { SourceIconBitmap = icon };
            }
        }
    }

    private ClipboardHistoryItem LocalizeItem(ClipboardHistoryItem item)
    {
        var typeLabel = item.DataType == ClipboardDataTypes.File
            ? string.Format(CultureInfo.CurrentCulture, _localization.Get("ClipboardDataType_File"), GetFileCount(item.Data))
            : _localization.Get($"ClipboardDataType_{item.DataType}");
        var itemInfo = item.DataType switch
        {
            ClipboardDataTypes.Text or ClipboardDataTypes.Color or ClipboardDataTypes.Url or ClipboardDataTypes.Json or ClipboardDataTypes.Xml or ClipboardDataTypes.Markdown => string.Format(
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
        var image = item.DataType == ClipboardDataTypes.Image ? item.ImageBitmap : null;
        Bitmap? sourceIcon = item.SourceIconBitmap;
        if (sourceIcon is null && !string.IsNullOrWhiteSpace(item.SourceIconPath))
        {
            _sourceIconCache.TryGetValue(item.SourceIconPath, out sourceIcon);
        }
        return item with { TypeLabel = typeLabel, ItemInfo = itemInfo, DisplayPreview = preview, ImageBitmap = image, SourceIconBitmap = sourceIcon };
    }

    private static bool IsTextLikeDataType(ClipboardHistoryItem item) =>
        item.DataType is ClipboardDataTypes.Text or ClipboardDataTypes.Color or ClipboardDataTypes.Url
            or ClipboardDataTypes.Json or ClipboardDataTypes.Xml or ClipboardDataTypes.Markdown;

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

    private static Bitmap? TryLoadSourceIcon(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            return null;
        }

        try
        {
            return new Bitmap(iconPath);
        }
        catch
        {
            return null;
        }
    }

    private static ClipboardDataTypes DetectTextDataType(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return ClipboardDataTypes.Text;
        }

        if (IsLikelyColor(trimmed))
        {
            return ClipboardDataTypes.Color;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return ClipboardDataTypes.Url;
        }

        if (LooksLikeJson(trimmed))
        {
            try
            {
                using var _ = JsonDocument.Parse(trimmed);
                return ClipboardDataTypes.Json;
            }
            catch
            {
                // fall through
            }
        }

        if (LooksLikeXml(trimmed))
        {
            try
            {
                _ = System.Xml.Linq.XDocument.Parse(trimmed);
                return ClipboardDataTypes.Xml;
            }
            catch
            {
                // fall through
            }
        }

        if (LooksLikeMarkdown(text))
        {
            return ClipboardDataTypes.Markdown;
        }

        return ClipboardDataTypes.Text;
    }

    private static bool IsLikelyColor(string text)
    {
        if (text.Length > 32)
        {
            return false;
        }

        if (text.StartsWith('#'))
        {
            return Color.TryParse(text, out _);
        }

        if (text.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("hsl(", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("hsla(", StringComparison.OrdinalIgnoreCase))
        {
            return Color.TryParse(text, out _);
        }

        return false;
    }

    private static bool LooksLikeJson(string text) =>
        (text.StartsWith('{') && text.EndsWith('}'))
        || (text.StartsWith('[') && text.EndsWith(']'));

    private static bool LooksLikeXml(string text) =>
        text.StartsWith('<') && text.EndsWith('>') && text.Contains("</", StringComparison.Ordinal);

    private static bool LooksLikeMarkdown(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var score = 0;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ")) score += 2;
            if (trimmed.StartsWith("## ")) score += 2;
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ ")) score += 1;
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\.\s")) score += 1;
            if (trimmed.StartsWith("> ")) score += 1;
            if (trimmed.StartsWith("```")) score += 2;
        }

        if (text.Contains("](", StringComparison.Ordinal)) score += 2;
        if (text.Contains("**") || text.Contains("__") || text.Contains("`")) score += 1;

        return score >= 2;
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
        var index = -1;
        for (var i = 0; i < History.Count; i++)
        {
            if (History[i].Id == original.Id)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            return;
        }

        if (MatchesCurrentFilter(updated))
        {
            History[index] = updated;
            var visibleIndex = VisibleHistory.ToList().FindIndex(item => item.Id == original.Id);
            if (visibleIndex >= 0)
            {
                VisibleHistory[visibleIndex] = updated;
            }
            else
            {
                RefreshVisibleHistory();
            }
        }
        else
        {
            History.RemoveAt(index);
            var visibleIndex = VisibleHistory.ToList().FindIndex(item => item.Id == original.Id);
            if (visibleIndex >= 0)
            {
                VisibleHistory.RemoveAt(visibleIndex);
            }
            EmptyHistoryText.IsVisible = VisibleHistory.Count == 0;
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
        _suppressDeactivateHideUntil = DateTimeOffset.UtcNow.AddMilliseconds(600);
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        // Bring to front without leaving the window always-on-top (which can hide dialogs).
        Topmost = true;
        Activate();
        Focus();
        Topmost = false;
    }

    private void ToggleFromHotkey()
    {
        if (IsVisible && IsActive)
        {
            Hide();
        }
        else if (IsVisible)
        {
            Topmost = true;
            Activate();
            Focus();
            Topmost = false;
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
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using OpenClipboard.App.Models;
using OpenClipboard.App.Services;

namespace OpenClipboard.App;

public partial class MainWindow : Window
{
    private readonly ClipboardStore _store = new();
    private readonly DispatcherTimer _clipboardTimer;
    private string? _lastClipboardText;
    private bool _isReadingClipboard;

    public ObservableCollection<ClipboardHistoryItem> History { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _clipboardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _clipboardTimer.Tick += ClipboardTimer_Tick;
        Opened += MainWindow_Opened;
        Closed += (_, _) => _clipboardTimer.Stop();
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        try
        {
            await _store.InitializeAsync();
            foreach (var item in await _store.GetRecentAsync())
            {
                History.Add(item);
            }

            _clipboardTimer.Start();
            StatusText.Text = "Monitoring text clipboard. Select an entry to copy it back.";
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
            var clipboard = Clipboard ?? throw new InvalidOperationException("The platform clipboard is unavailable.");
            using var data = await clipboard.TryGetDataAsync();
            var text = data is null ? null : await data.TryGetTextAsync();
            if (string.IsNullOrEmpty(text) || string.Equals(text, _lastClipboardText, StringComparison.Ordinal))
            {
                return;
            }

            var item = new ClipboardHistoryItem { Data = text };
            await _store.AddAsync(text);
            _lastClipboardText = text;
            History.Insert(0, item);
            StatusText.Text = "Monitoring text clipboard. Select an entry to copy it back.";
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

    private async void HistoryList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is not ClipboardHistoryItem item)
        {
            return;
        }

        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(item.Data));
            var clipboard = Clipboard ?? throw new InvalidOperationException("The platform clipboard is unavailable.");
            await clipboard.SetDataAsync(data);
            _lastClipboardText = item.Data;
            StatusText.Text = "Copied to clipboard.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not copy text: {exception.Message}";
        }

        HistoryList.SelectedItem = null;
    }
}
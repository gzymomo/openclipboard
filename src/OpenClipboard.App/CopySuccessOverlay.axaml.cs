using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace OpenClipboard.App;

public partial class CopySuccessOverlay : Window
{
    private readonly DispatcherTimer _closeTimer;

    public CopySuccessOverlay() : this("Copied")
    {
    }

    public CopySuccessOverlay(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            Close();
        };
        Opened += (_, _) =>
        {
            PositionOnPrimaryScreen();
            _closeTimer.Start();
        };
    }

    private void PositionOnPrimaryScreen()
    {
        var screen = Screens.Primary ?? Screens.ScreenFromWindow(this);
        if (screen is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        // Ensure layout is measured so ClientSize is available.
        InvalidateMeasure();
        Measure(new Avalonia.Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = DesiredSize;
        var scaling = screen.Scaling;
        var bounds = screen.Bounds;
        var pixelWidth = (int)Math.Round(size.Width * scaling);
        var pixelHeight = (int)Math.Round(size.Height * scaling);
        Position = new Avalonia.PixelPoint(
            bounds.X + (bounds.Width - pixelWidth) / 2,
            bounds.Y + (bounds.Height - pixelHeight) / 2);
    }
}

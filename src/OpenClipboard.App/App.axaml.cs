using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using OpenClipboard.App.Services;
using System;

namespace OpenClipboard.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.ShutdownRequested += (_, _) => window.AllowClose();

            var openItem = new NativeMenuItem("Open OpenClipboard");
            openItem.Click += (_, _) => window.ShowFromTray();
            var quitItem = new NativeMenuItem("Quit");
            quitItem.Click += (_, _) =>
            {
                window.AllowClose();
                desktop.Shutdown();
            };
            var menu = new NativeMenu();
            menu.Items.Add(openItem);
            menu.Items.Add(quitItem);

            using var iconStream = AssetLoader.Open(new Uri("avares://OpenClipboard.App/Assets/logo.png"));
            var icon = new TrayIcon
            {
                Icon = new WindowIcon(iconStream),
                ToolTipText = "OpenClipboard",
                Menu = menu,
                IsVisible = true
            };
            TrayIcon.SetIcons(this, new TrayIcons { icon });
        }

        base.OnFrameworkInitializationCompleted();
    }
}
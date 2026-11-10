using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using OpenClipboard.App.Services;
using System;
using System.Reflection;

namespace OpenClipboard.App;

public partial class App : Application
{
    public static string DisplayVersion { get; } =
        $"v{typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0"}";

    public ClipboardPreferences Preferences { get; private set; } = null!;

    public LocalizationService Localization { get; private set; } = null!;

    private NativeMenuItem? _openItem;
    private NativeMenuItem? _quitItem;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Preferences = new ClipboardPreferences();
            Localization = new LocalizationService(Preferences.Language);
            var window = new MainWindow(Preferences, Localization);
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.ShutdownRequested += (_, _) => window.AllowClose();

            _openItem = new NativeMenuItem();
            _openItem.Click += (_, _) => window.ShowFromTray();
            _quitItem = new NativeMenuItem();
            _quitItem.Click += (_, _) =>
            {
                window.AllowClose();
                desktop.Shutdown();
            };
            var menu = new NativeMenu();
            menu.Items.Add(_openItem);
            menu.Items.Add(_quitItem);
            ApplyLocalization();

            Program.StartActivationListener(() => Avalonia.Threading.Dispatcher.UIThread.Post(window.ShowFromTray));

            using var iconStream = AssetLoader.Open(new Uri("avares://OpenClipboard.App/Assets/logo.png"));
            var icon = new TrayIcon
            {
                Icon = new WindowIcon(iconStream),
                ToolTipText = $"OpenClipboard {DisplayVersion}",
                Menu = menu,
                IsVisible = true
            };
            TrayIcon.SetIcons(this, new TrayIcons { icon });
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ApplyLocalization()
    {
        if (Localization is null)
        {
            return;
        }

        if (_openItem is not null)
        {
            _openItem.Header = Localization.Get("CrossPlatform_OpenApp");
        }

        if (_quitItem is not null)
        {
            _quitItem.Header = Localization.Get("CrossPlatform_Quit");
        }
    }
}
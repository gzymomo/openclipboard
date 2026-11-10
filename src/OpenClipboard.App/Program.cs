using Avalonia;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OpenClipboard.App;

class Program
{
    private const string MutexName = "OpenClipboard.CrossPlatform.Singleton";
    private const int ActivationPort = 45821;
    private static Mutex? _instanceMutex;

    public static bool StartedAtLogin { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        StartedAtLogin = Array.Exists(args, argument => argument.Equals("--startup", StringComparison.Ordinal));

        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            SignalExistingInstance();
            _instanceMutex.Dispose();
            _instanceMutex = null;
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            try { _instanceMutex.ReleaseMutex(); } catch { }
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }
    }

    public static void StartActivationListener(Action activate)
    {
        var thread = new Thread(() =>
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, ActivationPort);
                listener.Start();
                while (true)
                {
                    using var client = listener.AcceptTcpClient();
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var message = reader.ReadLine();
                    if (string.Equals(message, "SHOW", StringComparison.Ordinal))
                    {
                        activate();
                    }
                }
            }
            catch
            {
                // Listener unavailable; single-instance guard via mutex still applies.
            }
        })
        {
            IsBackground = true,
            Name = "OpenClipboardActivationListener"
        };
        thread.Start();
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var client = new TcpClient();
            client.Connect(IPAddress.Loopback, ActivationPort);
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine("SHOW");
        }
        catch
        {
            // Existing instance may not be ready to receive activation; just avoid starting another instance.
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}

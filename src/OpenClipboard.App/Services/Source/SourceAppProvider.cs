using System;
using System.Threading.Tasks;
using OpenClipboard.App.Services.Source.Linux;
using OpenClipboard.App.Services.Source.Mac;
using OpenClipboard.App.Services.Source.Windows;

namespace OpenClipboard.App.Services.Source;

public sealed class SourceAppProvider : ISourceAppProvider
{
    private readonly ISourceAppProvider? _platformProvider = CreatePlatformProvider();

    public async Task<SourceAppInfo?> GetCurrentSourceAsync()
    {
        if (_platformProvider is null)
        {
            return null;
        }

        try
        {
            return await _platformProvider.GetCurrentSourceAsync().ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static ISourceAppProvider? CreatePlatformProvider()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsSourceProvider();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacSourceProvider();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxSourceProvider();
        }

        return null;
    }
}

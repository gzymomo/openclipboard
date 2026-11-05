using System.Reflection;
using Clipboard.Data.Enums;
using OpenClipboard.App;
using Xunit;

namespace OpenClipboard.App.Tests;

public sealed class AppVersionTests
{
    [Fact]
    public void DisplayVersionMatchesAssemblyInformationalVersion()
    {
        var assemblyVersion = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        Assert.NotNull(assemblyVersion);
        Assert.Equal($"v{assemblyVersion.InformationalVersion}", App.DisplayVersion);

        var dataAssemblyVersion = typeof(HistoryMode).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        Assert.NotNull(dataAssemblyVersion);
        Assert.Equal(assemblyVersion.InformationalVersion, dataAssemblyVersion.InformationalVersion);
    }
}

using System.Reflection;
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
    }
}

using OpenClipboard.App.Services;
using Xunit;

namespace OpenClipboard.App.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void LoadsEverySupportedLanguageAndFallsBackForNewCrossPlatformLabels()
    {
        var localization = new LocalizationService("zh-CN");

        Assert.Equal("设置", localization.Get("CrossPlatform_Settings"));
        Assert.NotEqual("SettingWindow_General_Language", localization.Get("SettingWindow_General_Language"));

        foreach (var language in localization.Languages)
        {
            localization.SetLanguage(language.Code);
            Assert.NotEqual("SettingWindow_General_Language", localization.Get("SettingWindow_General_Language"));
            if (language.Code != "en-US")
            {
                Assert.NotEqual("Save", localization.Get("CrossPlatform_Save"));
            }
        }
    }
}

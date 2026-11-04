using System.CodeDom.Compiler;
using System.Configuration;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Clipboard.Desktop.Properties;

[CompilerGenerated]
[GeneratedCode("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "16.8.1.0")]
internal sealed class Settings : ApplicationSettingsBase
{
	private static Settings defaultInstance = (Settings)SettingsBase.Synchronized(new Settings());

	public static Settings Default => defaultInstance;

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("")]
	public string Language
	{
		get
		{
			return (string)this["Language"];
		}
		set
		{
			this["Language"] = value;
		}
	}

	[DefaultSettingValue("{\"Control\":0,\"Vk\":113,\"IsEnable\":true}")]
	[DebuggerNonUserCode]
	[UserScopedSetting]
	public string ActivedHotkeyString
	{
		get
		{
			return (string)this["ActivedHotkeyString"];
		}
		set
		{
			this["ActivedHotkeyString"] = value;
		}
	}

	[DefaultSettingValue("")]
	[DebuggerNonUserCode]
	[UserScopedSetting]
	public string QuicklyCopyHotkeyString
	{
		get
		{
			return (string)this["QuicklyCopyHotkeyString"];
		}
		set
		{
			this["QuicklyCopyHotkeyString"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("1")]
	public int HistoryRecord
	{
		get
		{
			return (int)this["HistoryRecord"];
		}
		set
		{
			this["HistoryRecord"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("1")]
	public string IsFirstRun
	{
		get
		{
			return (string)this["IsFirstRun"];
		}
		set
		{
			this["IsFirstRun"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("60")]
	public int GlassOpacity
	{
		get
		{
			return (int)this["GlassOpacity"];
		}
		set
		{
			this["GlassOpacity"] = value;
		}
	}
}

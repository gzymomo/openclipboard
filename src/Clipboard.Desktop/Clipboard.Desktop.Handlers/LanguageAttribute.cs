using System;
using System.Runtime.CompilerServices;

namespace Clipboard.Desktop.Handlers;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
internal class LanguageAttribute : Attribute
{
	public string DisplayName { get; private set; }

	public string RegionCode { get; private set; }

	public LanguageAttribute(string displayName = "简体中文", string regionCode = "zh-CN", [CallerMemberName] string fieldName = null)
	{
		DisplayName = displayName;
		RegionCode = regionCode;
	}
}

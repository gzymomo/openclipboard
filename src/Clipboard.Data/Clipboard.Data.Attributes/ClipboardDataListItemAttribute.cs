using System;
using Clipboard.Data.Enums;
using Clipboard.Data.Interfaces;

namespace Clipboard.Data.Attributes;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class ClipboardDataListItemAttribute : Attribute, IClipboardDataListItem
{
	public string ResourceKey { get; set; }

	public string ResourceValue { get; set; }

	public string Source { get; set; }

	public ClipboardDataListTypes Type { get; set; }

	public ClipboardDataListItemAttribute(ClipboardDataListTypes data, string key, string value, string source)
	{
		ResourceKey = key;
		ResourceValue = value;
		Source = source;
		Type = data;
	}
}

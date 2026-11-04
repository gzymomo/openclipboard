using System;

namespace Clipboard.Data.Attributes;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class StringVertifyRegexAttribute : Attribute
{
	public string Pattern = "^.*$";

	public StringVertifyRegexAttribute(string pattern)
	{
		Pattern = pattern;
	}
}

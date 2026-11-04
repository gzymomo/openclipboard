using System;

namespace Clipboard.Desktop.Handlers;

public class LanguageModel : IComparable<LanguageModel>
{
	public int Val { get; set; } = -1;


	public string FieldName { get; set; } = string.Empty;


	public string DisplayName { get; set; } = string.Empty;


	public string RegionCode { get; set; } = string.Empty;


	public int CompareTo(LanguageModel compareModel)
	{
		return FieldName.CompareTo(compareModel.FieldName);
	}
}

using Clipboard.Data.Enums;

namespace Clipboard.Data.Interfaces;

public interface IClipboardDataListItem
{
	string ResourceKey { get; set; }

	string ResourceValue { get; set; }

	string Source { get; set; }

	ClipboardDataListTypes Type { get; set; }
}

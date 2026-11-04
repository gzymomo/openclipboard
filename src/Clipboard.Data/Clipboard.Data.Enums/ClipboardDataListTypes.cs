using Clipboard.Data.Attributes;

namespace Clipboard.Data.Enums;

public enum ClipboardDataListTypes
{
	[ClipboardDataListItem(ClipboardDataListTypes.All, "ClipboardDataListType_All", null, "")]
	All,
	[ClipboardDataListItem(ClipboardDataListTypes.Text, "ClipboardDataListType_Text", null, "")]
	Text,
	[ClipboardDataListItem(ClipboardDataListTypes.Image, "ClipboardDataListType_Image", null, "")]
	Image,
	[ClipboardDataListItem(ClipboardDataListTypes.File, "ClipboardDataListType_File", null, "")]
	File
}

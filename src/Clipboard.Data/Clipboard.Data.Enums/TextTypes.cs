using Clipboard.Data.Attributes;

namespace Clipboard.Data.Enums;

public enum TextTypes
{
	[StringVertifyRegex(".*")]
	Text,
	[StringVertifyRegex("^([hH][tT]{2}[pP]:\\/\\/|[hH][tT]{2}[pP][sS]:\\/\\/)(([A-Za-z0-9-~]+)\\.)+([A-Za-z0-9-~\\/])+$")]
	HttpLink,
	[StringVertifyRegex("^#([0-9a-fA-F]{2})?[0-9a-fA-F]{6}$")]
	Color
}

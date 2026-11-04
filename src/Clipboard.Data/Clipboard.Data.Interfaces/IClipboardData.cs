using System;
using Clipboard.Data.Enums;

namespace Clipboard.Data.Interfaces;

public interface IClipboardData
{
	string ID { get; }

	ClipboardDataTypes DataType { get; set; }

	string Data { get; set; }

	bool IsFavorite { get; set; }

	bool IsDeleted { get; set; }

	DateTime CreateTime { get; set; }

	DateTime LastTime { get; set; }

	DateTime DestructionTime { get; set; }

	int UsageCounter { get; set; }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Clipboard.Data.Enums;
using Clipboard.Data.Interfaces;

namespace Common.SQLitelib.DataModels;

[Table("ClipboardData")]
public class ClipboardData : IClipboardData
{
	[Key]
	public string ID { get; set; } = Guid.NewGuid().ToString("N");


	public ClipboardDataTypes DataType { get; set; }

	public string Data { get; set; }

	public bool IsFavorite { get; set; }

	public bool IsDeleted { get; set; }

	public DateTime CreateTime { get; set; } = DateTime.Now;


	public DateTime LastTime { get; set; } = DateTime.Now;


	public DateTime DestructionTime { get; set; }

	public int UsageCounter { get; set; }

	public ClipboardData()
	{
	}

	public ClipboardData(IClipboardData data)
	{
		ID = data.ID;
		DataType = data.DataType;
		Data = data.Data;
		IsFavorite = data.IsFavorite;
		IsDeleted = data.IsDeleted;
		CreateTime = data.CreateTime;
		UsageCounter = data.UsageCounter;
	}
}

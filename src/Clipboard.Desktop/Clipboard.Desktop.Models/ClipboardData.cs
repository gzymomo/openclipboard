using System;
using System.ComponentModel;
using Clipboard.Data.Enums;
using Clipboard.Data.Interfaces;
using Common.Helpers;
using Newtonsoft.Json;

namespace Clipboard.Desktop.Models;

public class ClipboardData : IClipboardData
{
	public string ID { get; private set; } = Guid.NewGuid().ToString("N");


	public ClipboardDataTypes DataType { get; set; }

	public string Data { get; set; }

	public bool IsFavorite { get; set; }

	public bool IsDeleted { get; set; }

	public DateTime CreateTime { get; set; }

	public DateTime LastTime { get; set; }

	public DateTime DestructionTime { get; set; }

	public int UsageCounter { get; set; }

	[DefaultValue(false)]
	public bool IsSelected { get; set; }

	public object GetFormatData => new Lazy<object>(delegate
	{
		object result = null;
		switch (DataType)
		{
		case ClipboardDataTypes.Text:
			result = Data;
			break;
		case ClipboardDataTypes.Image:
			result = Data.Base642ImageSource();
			break;
		case ClipboardDataTypes.File:
			result = JsonConvert.DeserializeObject<string[]>(Data);
			break;
		}
		return result;
	}).Value;

	public ClipboardData()
	{
		DataType = ClipboardDataTypes.Unknown;
		IsFavorite = false;
		IsDeleted = false;
		CreateTime = DateTime.Now;
		LastTime = DateTime.Now;
		UsageCounter = 0;
	}

	public ClipboardData(IClipboardData data)
	{
		ID = data.ID;
		DataType = data.DataType;
		Data = data.Data;
		IsFavorite = data.IsFavorite;
		IsDeleted = data.IsDeleted;
		CreateTime = data.CreateTime;
		LastTime = data.LastTime;
		DestructionTime = data.DestructionTime;
		UsageCounter = data.UsageCounter;
	}
}

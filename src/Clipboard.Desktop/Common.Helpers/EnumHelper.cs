using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Common.Helpers;

public static class EnumHelper
{
	public static string GetDescription(this Enum enumValue)
	{
		string text = enumValue.ToString();
		object[] customAttributes = enumValue.GetType().GetField(text).GetCustomAttributes(typeof(DescriptionAttribute), inherit: true);
		if (customAttributes.Length == 0)
		{
			return text;
		}
		return ((DescriptionAttribute)customAttributes[0]).Description;
	}

	public static T GetAttributeOfType<T>(this Enum @enum) where T : Attribute
	{
		object[] customAttributes = @enum.GetType().GetMember(@enum.ToString())[0].GetCustomAttributes(typeof(T), inherit: false);
		if (customAttributes.Length == 0)
		{
			return null;
		}
		return (T)customAttributes[0];
	}

	public static IEnumerable<(int, T)> GetEnumCustomAttributesList<T>(this Type @enum) where T : Attribute
	{
		if (!@enum.IsEnum)
		{
			return null;
		}
		List<(int, T)> list = new List<(int, T)>();
		foreach (object value in Enum.GetValues(@enum))
		{
			if (value is Enum enum2)
			{
				list.Add(((int)value, enum2.GetAttributeOfType<T>()));
			}
		}
		return list;
	}
}

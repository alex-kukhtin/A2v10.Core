// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace A2v10.Xaml.Report;

[TypeConverter(typeof(LengthConverter))]
public record Length
{
	public Single Value { get; init; } = 1;
	public String Unit { get; init; } = "fr";

	static readonly String[] ValidLength = ["mm", "cm", "pt", "in", "fr"];

	public Boolean IsEmpty()
	{
		return Value == 0;
	}

	public static Length Empty()
	{
		return new Length() { Value = 0, Unit = "pt" };
	}

	public static Length FromString(String strVal)
	{
		strVal = strVal.Trim().ToLowerInvariant().Replace("*", "fr");
		if (Single.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out Single tryResult))
			return new Length() { Value = tryResult, Unit = "pt" };
		var ext = strVal.Substring(strVal.Length - 2, 2);
		var val = strVal[..^2];
		if (ValidLength.Any(x => x == ext) && Single.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Single snglVal))
			return new Length() { Value = snglVal, Unit = ext };
		throw new XamlException($"Invalid length value '{strVal}'");
	}

	public override string ToString()
	{
		if (Unit == "pt")
			return Value.ToString(CultureInfo.InvariantCulture);
		FormattableString fs = $"{Value}{Unit}";
		return fs.ToString(CultureInfo.InvariantCulture);
	}
}

public class LengthConverter : TypeConverter
{
	public override Boolean CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		if (sourceType == typeof(String))
			return true;
		else if (sourceType == typeof(Length))
			return true;
		return false;
	}

	public override Object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, Object value)
	{
		if (value == null)
			return null;
		if (value is String strVal)
			return Length.FromString(strVal);
		throw new XamlException($"Invalid Length value '{value}'");
	}
}

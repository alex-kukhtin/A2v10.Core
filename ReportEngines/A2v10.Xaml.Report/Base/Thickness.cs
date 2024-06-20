// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.ComponentModel;
using System.Globalization;

namespace A2v10.Xaml.Report;

[TypeConverter(typeof(ThicknessConverter))]
public record Thickness
{
	public Length Top { get; init; } = Length.Empty();
	public Length Right { get; set; } = Length.Empty();
	public Length Bottom { get; set; } = Length.Empty();
	public Length Left { get; init; } = Length.Empty();

	public static Thickness FromString(String str)
	{
		if (String.IsNullOrEmpty(str))
			return new Thickness();
		var elems = str.Split(',');
		if (elems.Length == 1)
		{
			var t_all = Length.FromString(elems[0].Trim());
			return new Thickness()
			{
				Top = t_all,
				Left = t_all,
				Right = t_all,
				Bottom = t_all
			};
		}
		else if (elems.Length == 2)
		{
			var t_vert = Length.FromString(elems[0]);
			var t_horz = Length.FromString(elems[1]);
			return new Thickness()
			{
				Top = t_vert,
				Bottom = t_vert,
				Left = t_horz,
				Right = t_horz
			};
		}
		else if (elems.Length == 4)
		{
			return new Thickness()
			{
				Top = Length.FromString(elems[0]),
				Right = Length.FromString(elems[1]),
				Bottom = Length.FromString(elems[2]),
				Left = Length.FromString(elems[3])
			};
		}
		else
		{
			throw new XamlException($"Invalid Thickness value '{str}'");
		}
	}

	public Length? Vertical()
	{
		if (Top.Value == Bottom.Value && Top.Unit == Bottom.Unit)
			return Top;
		return null;
	}

	public Length? Horizontal()
	{
		if (Left.Value == Right.Value && Left.Unit == Right.Unit)
			return Left;
		return null;
	}

	public Length? All()
	{
		var v = Vertical();
		var h = Horizontal();
		if (v != null && h != null)
		{
			if (v.Value == h.Value && v.Unit == h.Unit)
				return v;
		}
		return null;
	}

	public override String ToString()
	{
		var t = All();
		if (t != null)
			return t.ToString();
		var h = Horizontal();
		var v = Vertical();
		if (v != null && h != null)
		{
			FormattableString fs = $"{v},{h}";
			return fs.ToString(CultureInfo.InvariantCulture);
		}
		FormattableString fs1 = $"{Top},{Right},{Bottom},{Left}";
		return fs1.ToString(CultureInfo.InvariantCulture);
	}
}

public class ThicknessConverter : TypeConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		if (sourceType == typeof(String))
			return true;
		else if (sourceType == typeof(Thickness))
			return true;
		return false;
	}

	public override Object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, Object value)
	{
		if (value == null)
			return null;
		if (value is String strVal)
			return Thickness.FromString(strVal);
		throw new XamlException($"Invalid Thickness value '{value}'");
	}
}

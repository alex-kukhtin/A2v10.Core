// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System.ComponentModel;
using System.Globalization;

namespace A2v10.Xaml;

public enum ToastStyle
{
	Success,
	Info,
	Warning,
	Danger
}

[TypeConverter(typeof(ToastConverter))]
public class Toast : XamlElement
{
	public String? Text { get; set; }
	public ToastStyle Style { get; set; }

	internal String GetJsValue(RenderContext context)
	{
		String? text = GetBindingString(context, nameof(Text), Text);
		String style = Style.ToString().ToLowerInvariant();
		return $"{{text: {text}, style: '{style}'}}";
	}
}

public class ToastConverter : TypeConverter
{
	public override Boolean CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		if (sourceType == typeof(String))
			return true;
		else if (sourceType == typeof(Toast))
			return true;
		return false;
	}

	public override Object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
	{
		if (value == null)
			return null;
		if (value is String strVal)
			return new Toast() { Text = strVal, Style = ToastStyle.Success };
		else if (value is Toast toastVal)
			return toastVal;
		throw new XamlException($"Invalid Toast value '{value}'");
	}
}

// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

using A2v10.System.Xaml;

namespace A2v10.Xaml.Report;

public enum TextStyle
{
	Default,
	Title
}

[ContentProperty("Inlines")]
public class Text : FlowElement
{
	public InlineCollection Inlines { get; set; } = new InlineCollection();

	public TextStyle Style { get; init; }

	public override void ApplyStyles(String selector, StyleBag styles)
	{
		var sel = $"Text.{Style}";
		_runtimeStyle = styles.GetRuntimeStyle(sel);
		ApplyStylesSelf();
		foreach (var inl in Inlines)
		{
			if (inl is XamlElement inlXaml)
				inlXaml.ApplyStyles(sel + ">Span", styles);
		}
	}
}

public class Span : ContentElement
{
	public override void ApplyStyles(String selector, StyleBag styles)
	{
		_runtimeStyle = styles.GetRuntimeStyle(selector);
		ApplyStylesSelf();
	}
}

public class Space : ContentElement
{
	public Length? Width { get; init; }
}

public class Break : ContentElement
{
	public Break()
	{
		this.Content = "\n";
	}
}


[TypeConverter(typeof(InlineCollectionConverter))]
public sealed class InlineCollection : List<Object>
{
}


public class InlineCollectionConverter : TypeConverter
{
	public override Boolean CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		if (sourceType == typeof(String))
			return true;
		else if (sourceType == typeof(InlineCollection))
			return true;
		return base.CanConvertFrom(context, sourceType);
	}

	public override Object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, Object value)
	{
		if (value == null)
			return null;
		if (value is String vStr)
		{
			var x = new InlineCollection() 
			{
				new Span() {Content = vStr }
			};
			return x;
		}
		else if (value is InlineCollection)
		{
			return value;
		}
		return base.ConvertFrom(context, culture, value);
	}
}

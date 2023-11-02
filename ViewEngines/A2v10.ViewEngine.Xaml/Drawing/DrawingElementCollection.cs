// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace A2v10.Xaml.Drawing;

[TypeConverter(typeof(DrawintCollectionConverter))]
public class DrawingElementCollection : List<DrawingElement>
{
	public DrawingElementCollection()
	{

	}
}

public class DrawintCollectionConverter : TypeConverter
{
	public override Boolean CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		if (sourceType == typeof(DrawingElement))
			return true;
		return base.CanConvertFrom(context, sourceType);
	}

	public override Object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, Object value)
	{
		if (value == null)
			return null;
		if (value is DrawingElement drawingElement)
		{
			var x = new DrawingElementCollection
			{
				drawingElement
			};
			return x;
		}
		return base.ConvertFrom(context, culture, value);
	}
}


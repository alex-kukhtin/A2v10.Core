// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace A2v10.Xaml;

[TypeConverter(typeof(UICollectionConverter))]
public class UIElementCollection : List<UIElementBase>
{
	public UIElementCollection()
	{
	}
}

public class UICollectionConverter : TypeConverter
{
	public override Boolean CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		if (sourceType == typeof(String))
			return true;
		else if (sourceType == typeof(UIElementBase))
			return true;
		return base.CanConvertFrom(context, sourceType);
	}

	public override Object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, Object value)
	{
		return value switch
		{
			null => null,
			UIElementBase uiElemBase => new UIElementCollection() { uiElemBase },
			String strVal => new UIElementCollection() { new Span() { Content = strVal } },
			_ => base.ConvertFrom(context, culture, value)

		};
	}
}


// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace A2v10.Xaml.Report;

public class TableColumn : XamlElement
{
	public Length Width { get; set; } = new Length();

	public static TableColumn FromString(String value)
	{
		return new TableColumn()
		{
			Width = Length.FromString(value)
		};
	}
}

[TypeConverter(typeof(TableColumnCollectionConverter))]
public class TableColumnCollection : List<TableColumn>
{
	public class TableColumnCollectionConverter : TypeConverter
	{
		public override Boolean CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
		{
			if (sourceType == typeof(String))
				return true;
			else if (sourceType == typeof(TableColumnCollection))
				return true;
			return false;
		}

		public override Object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, Object value)
		{
			if (value == null)
				return null;
			else if (value is TableColumnCollection)
				return value;
			if (value is String strVal)
			{
				var vals = strVal.Split(',');
				var coll = new TableColumnCollection();
				foreach (var val in vals)
				{
					coll.Add(TableColumn.FromString(val.Trim()));
				}
				return coll;
			}
			throw new XamlException($"Invalid value '{value}'");
		}
	}
}

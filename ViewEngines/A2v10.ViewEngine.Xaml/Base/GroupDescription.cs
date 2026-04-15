// Copyright © 2015-2026 Oleksandr Kukhtin. All rights reserved.

using System.Linq;
using System.Text;

using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

using A2v10.Infrastructure;

namespace A2v10.Xaml;
public class GroupDescription : XamlElement, IJavaScriptSource
{
	public String? GroupBy { get; set; }
	public Boolean Count { get; set; }
	public Boolean Collapsed { get; set; }
	public String? Title { get; set; }

	public String GetJsValue(RenderContext context)
	{
		if (String.IsNullOrEmpty(GroupBy))
			throw new XamlException("GroupBy property is required");
		var sb = new StringBuilder($"{{prop: '{GroupBy.EncodeJs()}'");
		if (Collapsed)
			sb.Append(", expanded:").Append((!Collapsed).ToString().ToLowerInvariant());
		if (Count)
			sb.Append(", count: true");
		if (!String.IsNullOrEmpty(Title))
			sb.Append($", title: '{Title.EncodeJs()}' ");
		sb.Append('}');
		return sb.ToString();
	}
}

[ContentProperty("Items")]
[TypeConverter(typeof(GroupDescriptionsConverter))]
public class GroupDescriptions : List<GroupDescription>, IJavaScriptSource
{
	public String? GetJsValue(RenderContext context)
	{
		if (Count == 0)
			return null;
		StringBuilder sb = new("[");
		foreach (var d in this)
		{
			sb.Append(d.GetJsValue(context)).Append(',');
		}
		sb.RemoveTailComma();
		sb.Append(']');
		return sb.ToString();
	}

	public List<GroupDescription> Items => this;
}

internal class GroupDescriptionsConverter : TypeConverter
{
	public override Boolean CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		if (sourceType == typeof(String))
			return true;
		return false;
	}

	public override Object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, Object value)
	{
		if (value == null)
			return null;
		if (value is String strVal)
		{
			var items = strVal.Split(',').Select(s => new GroupDescription() { GroupBy = s.Trim(), Count = true });
			GroupDescriptions descr = [..items];
			return descr;
		}
		throw new XamlException($"Invalid GroupDescriptions value '{value}'");
	}
}


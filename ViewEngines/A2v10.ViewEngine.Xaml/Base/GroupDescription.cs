// Copyright © 2015-2026 Oleksandr Kukhtin. All rights reserved.

using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.ComponentModel;

using A2v10.Infrastructure;

namespace A2v10.Xaml;
public class GroupDescription : XamlElement, IJavaScriptSource
{
	public String? GroupBy { get; init; }
	public Boolean? Count { get; init; }
	public Boolean Collapsed { get; init; }
	public String? Title { get; init; }

	public String GetJsValue(RenderContext context)
	{
		if (String.IsNullOrEmpty(GroupBy))
			throw new XamlException("GroupBy property is required");

		IEnumerable<String> props()
		{
			yield return $"prop: '{GroupBy.EncodeJs()}'";
            if (Count.HasValue && !Count.Value)
                yield return "count: false"; // true is default
            if (!String.IsNullOrEmpty(Title))
                yield return $"title: '{Title.EncodeJs()}'";
        }

		return $$"""{{{String.Join(',', props())}}}""";
	}
}

[ContentProperty("Items")]
[TypeConverter(typeof(GroupDescriptionsConverter))]
public class GroupDescriptions : IJavaScriptSource
{
	public List<GroupDescription> Items { get; set; } = [];
	public String? GetJsValue(RenderContext context) => 
		Items.Count > 0 ? $"""[{String.Join(',', Items.Select(x => x.GetJsValue(context)))}]""" : null;

}

internal class GroupDescriptionsConverter : TypeConverter
{
	public override Boolean CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
		=> sourceType == typeof(String);

	public override Object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, Object value)
	{
		if (value == null)
			return null;
		if (value is not String strVal)
            throw new XamlException($"Invalid GroupDescriptions value '{value}'");

        var items = strVal.Split(',').Select(s => new GroupDescription() { GroupBy = s.Trim(), Count = true });

		return new GroupDescriptions()
		{
			Items = [..items]
		};
	}
}


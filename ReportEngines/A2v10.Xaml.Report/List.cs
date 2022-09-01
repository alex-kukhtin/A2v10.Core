// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

using A2v10.System.Xaml;

namespace A2v10.Xaml.Report;

public class ListItem : ContentElement
{
	public Object? Bullet { get; set; }

	public override void ApplyStyles(string selector, StyleBag styles)
	{
		var sel = selector + ">ListItem";
		_runtimeStyle = styles.GetRuntimeStyle(sel);
		ApplyStylesSelf();
		if (Content is XamlElement contXaml)
		{
			contXaml.ApplyStyles("", styles);
		}
	}
}

public class ListItemCollection : List<ListItem>
{
}

[ContentProperty("Items")]
public class List : FlowElement
{
	public Object? ItemsSource { get; init; }

	public Single Spacing { get; init; }

	public ListItemCollection Items { get; init; } = new ListItemCollection();

	public override void ApplyStyles(string selector, StyleBag styles)
	{
		var sel = $"List";
		_runtimeStyle = styles.GetRuntimeStyle(sel);
		ApplyStylesSelf();
		foreach (var item in Items)
			item.ApplyStyles(sel, styles);
	}
}

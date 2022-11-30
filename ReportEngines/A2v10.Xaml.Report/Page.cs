// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.System.Xaml;

namespace A2v10.Xaml.Report;

[ContentProperty("Columns")]
public class Page : XamlElement
{
	public String? Title { get; init; }
	public String? Code { get; init; }
	public ColumnCollection Columns { get; set; } = new ColumnCollection();
	public Column? Header { get; init; }
	public Column? Footer { get; init; }
	public String? FontFamily { get; init; }
	public PageOrientation Orientation { get; init; }

	public override void ApplyStyles(String selector, StyleBag styles)
	{
		var sel = "Page";
		_runtimeStyle = styles.GetRuntimeStyle(sel);
		foreach (var col in Columns)
			col.ApplyStyles(sel, styles);
		Header?.ApplyStyles(sel, styles);
		Footer?.ApplyStyles(sel, styles);
		ApplyStylesSelf();
	}
}

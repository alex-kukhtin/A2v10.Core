// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.System.Xaml;

namespace A2v10.Xaml.Report;

public enum TableStyle
{
	Default,
	Details,
	Simple
}

[ContentProperty("Body")]
public class Table : FlowElement
{
	public Object? ItemsSource { get; init; }

	public TableColumnCollection Columns { get; set; } = [];

	public TableRowCollection Header { get; set; } = [];

	public TableRowCollection Footer { get; set; } = [];

	public TableRowCollection Body { get; set; } = [];

	public TableStyle Style { get; init; }

	public override void ApplyStyles(string selector, StyleBag styles)
	{
		var sel = $"Table.{Style}";
		_runtimeStyle = styles.GetRuntimeStyle(sel);
		foreach (var h in Header)
			h.ApplyStyles(sel + ">Header", styles);
		foreach (var b in Body)
			b.ApplyStyles(sel + ">Body", styles);
		foreach (var f in Footer)
			f.ApplyStyles(sel + ">Footer", styles);
		ApplyStylesSelf();
	}
}

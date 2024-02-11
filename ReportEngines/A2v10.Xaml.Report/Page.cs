// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Text.Json.Serialization;
using A2v10.System.Xaml;

namespace A2v10.Xaml.Report;

[ContentProperty("Columns")]
public class Page : XamlElement
{
	public String? Title { get; set; }
	public String? Code { get; init; }

	[JsonIgnore]
	public ColumnCollection Columns { get; init; } = [];
	[JsonIgnore]
	public Column? Header { get; init; }
	[JsonIgnore]
	public Column? Footer { get; init; }

	public String? FontFamily { get; init; }
	public PageOrientation Orientation { get; set; }

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

// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Xaml.Report.Spreadsheet;

public class Workbook : XamlElement
{
    public UInt32 RowCount { get; set; }
	public UInt32 ColumnCount { get; set; }
	public ColumnCollection Columns { get; init; } = [];
	public RowCollection Rows { get; init; } = [];
	public CellCollection Cells { get; init; } = [];
	public RangeCollection Ranges { get; init; } = [];	
	public StyleCollection Styles { get; init; } = [];
	public Range? Header { get; set; }
	public Range? Footer { get; set; }
	public Range? TableHeader { get; set; }
	public Range? TableFooter { get; set; }	
	public PageFooter? PageFooter { get; set; }
	public override void ApplyStyles(String selector, StyleBag styles)
    {
		var sel = selector;
		_runtimeStyle = styles.GetRuntimeStyle(sel);
		foreach (var c in Cells.Values)
		{
			c.OnCreate();
			c.ApplyStyles(sel + ">Cell", styles);
			if (c.Style != null && Styles.TryGetValue(c.Style, out var runtimeStyle))
				c.ApplyRuntimeStyle(runtimeStyle);
		}
		foreach (var r in Rows.Values)
			r.ApplyStyles(sel + ">Row", styles);
		foreach (var c in Columns.Values)
			c.ApplyStyles(sel + ">Column", styles);
		ApplyStylesSelf();
	}
}

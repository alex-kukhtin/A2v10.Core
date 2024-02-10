// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Xaml.Report.Spreadsheet;

public class Workbook : XamlElement
{
    public Int32 RowCount { get; init; }
	public Int32 ColumnCount { get; init; }
	public ColumnCollection Columns { get; init; } = [];
	public RowCollection Rows { get; init; } = [];
	public CellCollection Cells { get; init; } = [];
	public RangeCollection Ranges { get; init; } = [];	
	public override void ApplyStyles(string selector, StyleBag styles)
    {
        base.ApplyStyles(selector, styles);
    }
}

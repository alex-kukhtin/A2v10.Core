// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Xaml.Report.Spreadsheet;

public class Cell
{
	public String? Value { get; init; }
	public Int32 RowSpan { get; init; }
	public Int32 ColSpan { get; init; }
}

public class CellCollection : Dictionary<String, Cell>
{
}

// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Xaml.Report.Spreadsheet;

public class Range
{
	public String Value { get; init; } = String.Empty;
	public Int32 Start { get; init; }
	public Int32 End { get; init; }
}

public class RangeCollection : List<Range>
{
}

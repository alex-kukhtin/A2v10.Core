// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Xaml.Report.Spreadsheet;

public class Row
{
	public Int32 Height { get; init; }
}

public class RowCollection : Dictionary<Int32, Row>
{
}

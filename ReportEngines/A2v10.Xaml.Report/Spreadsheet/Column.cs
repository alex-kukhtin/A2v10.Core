// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Xaml.Report.Spreadsheet;

public class Column : XamlElement
{
	public Single Width { get; init; }
}

public class ColumnCollection : Dictionary<String, Column>
{
}

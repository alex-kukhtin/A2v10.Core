// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Xaml.Report.Spreadsheet;

public class Row : XamlElement
{
	public Single Height { get; init; }
}

public class RowCollection : Dictionary<UInt32, Row>
{
}

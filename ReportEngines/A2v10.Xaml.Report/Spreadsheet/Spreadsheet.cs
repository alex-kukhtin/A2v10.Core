// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Xaml.Report.Spreadsheet;

public class Spreadsheet : Page
{
	public Workbook Workbook { get; init; } = new();

	public override void ApplyStyles(String selector, StyleBag styles)
	{
		var sel = "Spreadsheet";
		_runtimeStyle = styles.GetRuntimeStyle(sel);
		Workbook.ApplyStyles(sel + ">Workbook", styles);
		ApplyStylesSelf();
	}
}

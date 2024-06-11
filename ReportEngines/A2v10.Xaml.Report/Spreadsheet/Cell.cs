// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Xaml.Report.Spreadsheet;

public class Cell : XamlElement
{
	public String? Value { get; set; }
	public UInt32 RowSpan { get; set; }
	public UInt32 ColSpan { get; set; }
	public DataType DataType { get; init; }
	public String? Style { get; init; }
	public String? Format { get; init; }	
	public void ApplyRuntimeStyle(RuntimeStyle style)
	{
		var rs = GetRuntimeStyle();
		
		if (style.Border != null)
			rs.Border = style.Border;
		if (style.Background != null)
			rs.Background = style.Background;
		if (style.FontSize != null)
			rs.FontSize = style.FontSize;
		if (style.Align != null)
			rs.Align = style.Align;
		if (style.VAlign != null)
			rs.VAlign = style.VAlign;
		if (style.Padding != null)
			rs.Padding = style.Padding;
		if (style.Margin != null)	
			rs.Margin = style.Margin;
		if (style.Bold != null)
			rs.Bold = style.Bold;
		if (style.Italic != null)
			rs.Italic = style.Italic;
		if (style.Underline != null)
			rs.Underline = style.Underline;
		if (style.Color != null)
			rs.Color = style.Color;
		if (style.Format != null)	
			rs.Format = style.Format;
		if (style.TextRotation != null)
			rs.TextRotation = style.TextRotation;
	}

	public void OnCreate()
	{
		var rs = GetRuntimeStyle();
		rs.Padding = Thickness.FromString("1,3,2,3"); // 1pt t, 3pt l+r, 2pt b
		rs.VAlign = VertAlign.Bottom; // default is bottom
		switch (DataType)
		{
			case DataType.Date:
			case DataType.Time:
			case DataType.DateTime:
				rs.Align = TextAlign.Center;
				break;
			case DataType.Number:
			case DataType.Currency:
				rs.Align = TextAlign.Right;
				break;

		}
	}
}

public class CellCollection : Dictionary<String, Cell>
{
}

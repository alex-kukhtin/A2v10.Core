﻿// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

using System.IO;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace A2v10.Services.Interop;

public class ExcelWriter
{
	private readonly List<String> _mergeCells = [];
	public Byte[] SheetToExcel(ExSheet exsheet)
	{
		var ms = new MemoryStream();
		using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook, true))
		{
			WorkbookPart wbPart = doc.AddWorkbookPart();
			wbPart.Workbook = new Workbook();
			WorksheetPart wsPart = wbPart.AddNewPart<WorksheetPart>();

			WorkbookStylesPart workStylePart = wbPart.AddNewPart<WorkbookStylesPart>();
			workStylePart.Stylesheet = AddStyles(exsheet.Styles);
			workStylePart.Stylesheet.Save();

			wsPart.Worksheet = GetDataFromSheet(exsheet);

			if (_mergeCells.Count > 0)
			{
				var mc = new MergeCells();
				foreach (var mergeRef in _mergeCells)
					mc.Append(new MergeCell() { Reference = mergeRef });
				wsPart.Worksheet.Append(mc);
			}

			wsPart.Worksheet.AddChild(new IgnoredErrors(
				new IgnoredError()
				{
					NumberStoredAsText = true,
					SequenceOfReferences = new ListValue<StringValue>(
						new List<StringValue>() { new("A1:WZZ999999") })
				}
			));

			Sheets sheets = wbPart.Workbook.AppendChild<Sheets>(new Sheets());
			Sheet sheet = new() { Id = wbPart.GetIdOfPart(wsPart), SheetId = 1, Name = "Sheet1" };
			sheets.Append(sheet);

			wbPart.Workbook.Save();
		};
		return ms.ToArray();
	}

	static Stylesheet AddStyles(StylesDictionary styles)
	{
		static Color autoColor() { return new Color() { Auto = true }; }

		var fonts = new Fonts(
			new Font( // Index 0 - default
				new FontSize() { Val = 11 }

			),
			new Font( // Index 1 - bold
				new FontSize() { Val = 11 },
				new Bold()
			),
			new Font( // Index 2 - title
				new FontSize() { Val = 14 },
				new Bold()
			));

		var borders = new Borders(
				new Border(), // index 0 default
				new Border(   // index 1 black border
					new LeftBorder(autoColor()) { Style = BorderStyleValues.Thin },
					new RightBorder(autoColor()) { Style = BorderStyleValues.Thin },
					new TopBorder(autoColor()) { Style = BorderStyleValues.Thin },
					new BottomBorder(autoColor()) { Style = BorderStyleValues.Thin },
					new DiagonalBorder()),
				new Border( // index 2 bottom border
					new LeftBorder(),
					new RightBorder(),
					new TopBorder(),
					new BottomBorder(autoColor()) { Style = BorderStyleValues.Thin },
					new DiagonalBorder())
			);

		var fills = new Fills(
				//index 0 - default
				new Fill(new PatternFill() { PatternType = PatternValues.None }),
				//index 1 - skip
				new Fill(new PatternFill()
				{
					PatternType = PatternValues.Gray0625,
				}),
				//index 2 - Light gold (total)
				new Fill(new PatternFill()
				{
					PatternType = PatternValues.Solid,
					ForegroundColor = new ForegroundColor() { Rgb = "FFFFfAD4" },
					BackgroundColor = new BackgroundColor() { Indexed = (UInt32Value)64U }
				}),
				//index 3 - Light gray (total)
				new Fill(new PatternFill()
				{
					PatternType = PatternValues.Solid,
					ForegroundColor = new ForegroundColor() { Rgb = "FFF8F8F8" },
					BackgroundColor = new BackgroundColor() { Indexed = (UInt32Value)64U }
				}),
				// index 4 -> LightGreen (group)
				new Fill(new PatternFill()
				{
					PatternType = PatternValues.Solid,
                    ForegroundColor = new ForegroundColor() { Rgb = "FFEFFCF6" },
                    BackgroundColor = new BackgroundColor() { Indexed = (UInt32Value)64U }
                })
            );

		var numFormats = new NumberingFormats(
				/*date*/     new NumberingFormat() { FormatCode = "dd\\.mm\\.yyyy;@", NumberFormatId = 166 },
				/*datetime*/ new NumberingFormat() { FormatCode = "dd\\.mm\\.yyyy hh:mm;@", NumberFormatId = 167 },
				/*currency*/ new NumberingFormat() { FormatCode = "#,##0.00####;[Red]\\-#,##0.00####", NumberFormatId = 169 },
				/*number*/   new NumberingFormat() { FormatCode = "#,##0.######;[Red]-#,##0.######", NumberFormatId = 170 },
				/*time*/     new NumberingFormat() { FormatCode = "hh:mm;@", NumberFormatId = 165 }
			);

		var cellFormats = new CellFormats(new CellFormat());

		for (var i = 1 /*1-based!*/; i < styles.List.Count; i++)
		{
			Style st = styles.List[i];
			cellFormats.Append(CreateCellFormat(st));
		}

		return new Stylesheet(numFormats, fonts, fills, borders, cellFormats);
	}

	static CellFormat CreateCellFormat(Style style)
	{
		var cf = new CellFormat()
		{
			FontId = 0,
			ApplyAlignment = true,
			Alignment = new Alignment
			{
				Vertical = VerticalAlignmentValues.Top
			}
		};


		// font
		if (style.RowRole == RowRole.Title)
		{
			cf.FontId = 2;
			cf.ApplyFont = true;
			cf.Alignment.WrapText = true;
		}
		else if (style.Bold)
		{
			cf.FontId = 1;
			cf.ApplyFont = true;
		}
		// dataType
		switch (style.DataType)
		{
			case DataType.Currency:
				cf.NumberFormatId = 169;
				cf.ApplyNumberFormat = true;
				break;
			case DataType.Date:
				cf.NumberFormatId = 166;
				cf.ApplyNumberFormat = true;
				break;
			case DataType.DateTime:
				cf.NumberFormatId = 167;
				cf.ApplyNumberFormat = true;
				break;
			case DataType.Time:
				cf.NumberFormatId = 165;
				cf.ApplyNumberFormat = true;
				break;
			case DataType.Percent:
				cf.NumberFormatId = 9; /*standard*/
				cf.ApplyNumberFormat = true;
				break;
			case DataType.Boolean:
				cf.NumberFormatId = 0; /*standard*/
				cf.ApplyNumberFormat = true;
				break;
			case DataType.Number:
				break;
			case DataType.String:
				cf.Alignment.WrapText = true;
				break;
		}
		// border
		if (style.HasBorder)
		{
			cf.BorderId = 1;
			cf.ApplyBorder = true;
		}

		// fill
		if (style.RowRole == RowRole.Header)
		{
			cf.FillId = 3;
			cf.ApplyFill = true;
		}
		else if (style.RowRole == RowRole.Total)
		{
			cf.FillId = 2;
			cf.ApplyFill = true;
		}

		if (style.IsGroup)
		{
			cf.FillId = 4; // зеленый
			cf.ApplyFill = true;
		}

		// align
		if (style.IsDateOrTime || style.IsBoolean)
			cf.Alignment.Horizontal = HorizontalAlignmentValues.Center;

		if (style.Wrap)
			cf.Alignment.WrapText = true;

        switch (style.Align)
		{
			case HorizontalAlign.Center:
				cf.Alignment.Horizontal = HorizontalAlignmentValues.Center;
				break;
			case HorizontalAlign.Right:
				cf.Alignment.Horizontal = HorizontalAlignmentValues.Right;
				break;
		}

		switch (style.VAlign)
		{
			case VerticalAlign.Middle:
				cf.Alignment.Vertical = VerticalAlignmentValues.Center;
				break;
			case VerticalAlign.Top:
				cf.Alignment.Vertical = VerticalAlignmentValues.Top;
				break;
			case VerticalAlign.Bottom:
				cf.Alignment.Vertical = VerticalAlignmentValues.Bottom;
				break;
		}

        if (style.Vertical)
        {
            cf.Alignment.TextRotation = 90;
			cf.Alignment.Horizontal = HorizontalAlignmentValues.Center;
			cf.Alignment.Vertical = VerticalAlignmentValues.Bottom;
			cf.Alignment.WrapText = true;
        }

        if (style.Indent > 1)
			cf.Alignment.Indent = style.Indent - 1;
		if (style.Underline)
			cf.BorderId = 2;

		return cf;
	}

	Worksheet GetDataFromSheet(ExSheet sheet)
	{

		var sd = new SheetData();
		var cols = new Columns();

		ProcessColums(sheet, cols /*null*/);

		Int32 rowNo = 0;
		foreach (var row in sheet.Rows)
			sd.Append(ProcessRow(row, rowNo++));

		var props = new SheetFormatProperties()
		{
			BaseColumnWidth = 10,
			DefaultRowHeight = 30,
			DyDescent = 0.25
		};

		var ws = new Worksheet(props, cols, sd);
		return ws;
	}

	static void ProcessColums(ExSheet sheet, Columns columns /*, XmlNode source*/)
	{
		for (UInt32 c = 0; c < sheet.Columns.Count; c++)
		{
			var col = sheet.Columns[(Int32)c];
			if (col.Width != 0)
			{
				var w = ConvertUnit(col.Width);
				columns.Append(new Column() { Min = c + 1, Max = c + 1, BestFit = true, CustomWidth = true, Width = w });
			}
			else
				columns.Append(new Column() { Min = c + 1, Max = c + 1, Width = 11.5 }) ;
		}
	}

	static Double ConvertUnit(UInt32 val)
	{
		Decimal charWidth = 7;
		return (Double)Math.Truncate((val + 5L) / charWidth * 256L) / 256L;
	}

	static Double ConvertToPoints(UInt32 px)
	{
		Double rows = Math.Ceiling(px / 18.0);
		return Math.Round(rows * 15, 2);
	}

	Row ProcessRow(ExRow exrow, Int32 rowNo)
	{
		var row = new Row();
		if (exrow.Height != 0)
		{
			row.Height = ConvertToPoints(exrow.Height);
			row.CustomHeight = true;
		}
		else if (exrow.Role == RowRole.Divider)
		{
			row.Height = 8.25;
			row.CustomHeight = true;
		}
		for (var col = 0; col < exrow.Cells.Count; col++)
		{
			var c = exrow.Cells[col];
			if (c.Kind == CellKind.Null)
				continue;
			var cell = new Cell();
			SetCellValue(cell, c /*, exrow*/);
			cell.CellReference = ExCell.Reference(rowNo, col);
			var mergeRef = c.MergeReference(rowNo, col);
			if (mergeRef != null)
				_mergeCells.Add(mergeRef);
			row.Append(cell);
		}
		return row;
	}

	static void SetCellValue(Cell cell, ExCell exCell/*, ExRow exRow*/)
	{
		if (exCell.StyleIndex != 0)
			cell.StyleIndex = exCell.StyleIndex;
		if (exCell.Kind != CellKind.Normal)
			return;
		switch (exCell.DataType)
		{
			case DataType.String:
			case DataType.StringPlain:
				cell.DataType = new EnumValue<CellValues>(CellValues.InlineString);
				cell.InlineString = new InlineString(new Text(exCell.Value));
				break;
			case DataType.Currency:
				cell.DataType = new EnumValue<CellValues>(CellValues.Number);
				cell.CellValue = new CellValue(exCell.Value);
				break;
			case DataType.Number:
				cell.DataType = new EnumValue<CellValues>(CellValues.Number);
				cell.CellValue = new CellValue(exCell.Value);
				break;
			case DataType.Percent:
				cell.DataType = new EnumValue<CellValues>(CellValues.Number);
				cell.CellValue = new CellValue(exCell.Value);
				break;
			case DataType.Date:
			case DataType.DateTime:
				// DataType not needed
				cell.CellValue = new CellValue(exCell.Value);
				break;
			case DataType.Boolean:
				cell.DataType = new EnumValue<CellValues>(CellValues.Boolean);
				cell.CellValue = new CellValue(exCell.Value);
				break;
			default:
				cell.CellValue = new CellValue(exCell.Value);
				break;
		}
	}
}

﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace A2v10.Services.Interop.ExportTo;
public class Html2Excel
{
    private readonly IFormatProvider _currentFormat;

    public Html2Excel(String locale)
    {
        _currentFormat = System.Globalization.CultureInfo.CreateSpecificCulture(locale);
    }

    private readonly List<String> _mergeCells = new();

    public Byte[] ConvertHtmlToExcel(String html)
    {
        var rdr = new HtmlReader(_currentFormat);
        var sheet = rdr.ReadHtmlSheet(html);
        return SheetToExcel(sheet);
    }

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

            Sheets sheets = wbPart.Workbook.AppendChild<Sheets>(new Sheets());
            Sheet sheet = new() { Id = wbPart.GetIdOfPart(wsPart), SheetId = 1, Name = "Sheet1" };
            sheets.Append(sheet);

            wbPart.Workbook.Save();
            doc.Close();
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
                new Border( // index 1 black border
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
                new Fill(new PatternFill() { PatternType = PatternValues.None }));

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

        // align
        if (style.DataType == DataType.Date || style.DataType == DataType.DateTime)
        {
            cf.Alignment.Horizontal = HorizontalAlignmentValues.Center;
        }

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
        if (style.Indent > 1)
            cf.Alignment.Indent = style.Indent - 1;
        if (style.Underline)
            cf.BorderId = 2;
        return cf;
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
            case DataType.Date:
            case DataType.DateTime:
                // DataType not needed
                cell.CellValue = new CellValue(exCell.Value);
                break;
            default:
                cell.CellValue = new CellValue(exCell.Value);
                break;
        }
    }

    Row ProcessRow(ExRow exrow, Int32 rowNo)
    {
        var row = new Row();
        if (exrow.Height != 0)
        {
            row.Height = ConvertToPoints(exrow.Height);
            row.CustomHeight = true;
        }
        for (var col = 0; col < exrow.Cells.Count; col++)
        {
            var c = exrow.Cells[col];
            if (c.Kind == CellKind.Null)
                continue;
            var cell = new Cell();
            SetCellValue(cell, c /*, exrow*/);
            cell.CellReference = c.Reference(rowNo, col);
            var mergeRef = c.MergeReference(rowNo, col);
            if (mergeRef != null)
                _mergeCells.Add(mergeRef);
            row.Append(cell);
        }
        return row;
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
        }
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
}


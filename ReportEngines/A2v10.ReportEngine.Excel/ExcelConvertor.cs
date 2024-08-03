// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml;

using XSheet = A2v10.Xaml.Report.Spreadsheet.Spreadsheet;
using XWorkbook = A2v10.Xaml.Report.Spreadsheet.Workbook;
using XCell = A2v10.Xaml.Report.Spreadsheet.Cell;
using XRow = A2v10.Xaml.Report.Spreadsheet.Row;
using XColumn = A2v10.Xaml.Report.Spreadsheet.Column;
using XRange = A2v10.Xaml.Report.Spreadsheet.Range;
using XStyle = A2v10.Xaml.Report.RuntimeStyle;
using XPageFooter = A2v10.Xaml.Report.Spreadsheet.PageFooter;

using PageOrientation  = A2v10.Xaml.Report.PageOrientation;
using TextAlign = A2v10.Xaml.Report.TextAlign;
using VertAlign = A2v10.Xaml.Report.VertAlign;
using Thickness = A2v10.Xaml.Report.Thickness;

namespace A2v10.ReportEngine.Excel;

/* TODO:
13. Download as Excel (в обратную сторону)
15. Попробовать сделать еще одну странцу Scripts - и в нее можно вставить код на JS
 */

/*
Excel Column Width
	https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.column?view=openxml-3.0.1
*/

public class StyleRefs
{
	private readonly Border[] _borders;
	private readonly Font[] _fonts;
	private readonly Fill[] _fills;
	private FontSize? _defaultFontSize;
	private FontName? _defaultFontName;
	private readonly Dictionary<String, NumberingFormat> _formats;

	public String? DefaultFontFamily => _defaultFontName?.Val?.Value;
	public Single? DefaultFontSize => (Single?) _defaultFontSize?.Val?.Value;

	const String THIN_BORDER = ".2";
	const String MEDIUM_BORDER = "1";
	public StyleRefs(Stylesheet stylesheet)
	{
		_borders = stylesheet.Descendants<Border>().ToArray();
		_fonts = stylesheet.Descendants<Font>().ToArray();
		_fills = stylesheet.Descendants<Fill>().ToArray();

		_formats = stylesheet.Descendants<NumberingFormat>()
			.GroupBy(x => x.NumberFormatId?.Value.ToString() ?? String.Empty)
			.ToDictionary(g => g.Key, g => g.First());

		ParseDefaultFont(stylesheet);
	}

	void ParseDefaultFont(Stylesheet stylesheet) 
	{ 
		var defaultStyle = stylesheet.CellStyles?.Cast<CellStyle>().FirstOrDefault(s => s.Name == "Normal");
		if (defaultStyle == null)
			return;
		var formatId = defaultStyle.FormatId?.Value;
		if (formatId == null)
			return;
		var fmts = stylesheet.CellStyleFormats?.ToArray();
		if (fmts != null && fmts.Length > formatId)
		{
			if (fmts[formatId.Value] is CellFormat cf && cf.FontId != null)
			{
				var f = _fonts[cf.FontId.Value];
				_defaultFontSize = f.FontSize;
				_defaultFontName = f.FontName;
			}
		}
	}

	public Thickness? GetBorder(UInt32? id)
	{
		if (id == null)
			return null;
		var b = _borders[id.Value];
		if (b == null)
			return null;
		var bx =  $"{Length(b.TopBorder?.Style)},{Length(b.RightBorder?.Style)},{Length(b.BottomBorder?.Style)},{Length(b.LeftBorder?.Style)}";
		return Thickness.FromString(bx);
	}

	public String? GetFormat(UInt32Value? id)
	{
		if (id == null)
			return null;
		var f = ExcelFormats.GetNumberFormat(id);
		if (f != null)
			return f;
		if (id != null && _formats.TryGetValue(id.ToString()!, out NumberingFormat? nf) && nf != null)
			return nf.FormatCode?.Value;
		return null;
	}

	public (Single? fontSize, Boolean? bold, Boolean? italic, Boolean? underline, String? name)? GetFont(UInt32? id)
	{
		if (id == null)
			return null;
		var f = _fonts[id.Value];

		Boolean? bold = null;
		Boolean? italic = null;
		Boolean? underline = null;
		Single? fontSize = null;
		String? name = null;
		if (f.Bold != null)
			bold = true;
		if (f.Italic != null)
			italic = true;
		if (f.Underline != null)
			underline = true;
		if (f.FontSize?.Val != _defaultFontSize?.Val)
			fontSize = (Single?) f.FontSize?.Val?.Value;
		if (f.FontName?.Val != null && f.FontName?.Val != _defaultFontName?.Val)
			name = f.FontName?.Val?.Value;
		return (fontSize, bold, italic, underline, name);
	}

	public String? GetFill(UInt32? id)
	{
		if (id == null)
			return null;
		var f = _fills[id.Value - 1];
		if (f.PatternFill?.PatternType?.Value == PatternValues.Gray125)
			return "f5f5f5";
		return null;
	}
	private static String Length(EnumValue<BorderStyleValues>? val) 
	{
		if (val == null)
			return "0";
		if (val == BorderStyleValues.Thin)
			return THIN_BORDER;
		else if (val == BorderStyleValues.Medium)
			return MEDIUM_BORDER;
		throw new InvalidOperationException();
	}
}
public class ExcelConvertor
{
	private readonly String _fileName;
	private readonly Stream? _stream;
	private UInt32 _realColumns = 0;
	private UInt32 _realRows = 0;	

	private String? _fitColumn = null;

	private const Single CHAR_WIDTH = 8;

	public ExcelConvertor(String fileName)
	{
		_fileName = fileName;
		_stream = null;
	}

	public ExcelConvertor(Stream stream)
	{
		_fileName = String.Empty;
		_stream = stream;
	}

	public XSheet ParseFile()
	{

		var wb = new XWorkbook();
		var ws = new XSheet()
		{
			Workbook = wb
		};

		var doc = _stream != null ? SpreadsheetDocument.Open(_stream, isEditable: false)
			: SpreadsheetDocument.Open(_fileName, isEditable: false);
		var workBookPart = doc.WorkbookPart
			?? throw new InvalidOperationException("Invalid Excel file");
		var workBook = workBookPart.Workbook;
		var sheet = workBook.Descendants<Sheet>().First()
			?? throw new InvalidOperationException($"The workbook does not have a sheet");
		if (sheet.Id == null)
			throw new InvalidOperationException($"The workbook sheet not have an id");
		String sheetId = sheet.Id.Value
			?? throw new InvalidOperationException($"The workbook sheet not have an id");
		var workSheetPart = (WorksheetPart)workBookPart.GetPartById(sheetId);
		var sharedStringPart = workBookPart.SharedStringTablePart
			?? throw new InvalidOperationException($"The workbook does not have a SharedStringTablePart");
		var sharedStringTable = sharedStringPart.SharedStringTable
			?? throw new InvalidOperationException($"The SharedStringTablePart does not have a SharedStringTable");

		var stylesPart = workBookPart.WorkbookStylesPart;
		var styleSheet = stylesPart?.Stylesheet;

		var dim = workSheetPart.Worksheet.SheetDimension?.Reference?.ToString()
			?? throw new InvalidOperationException("Invalid SheetDimension");
		var dimSplit = dim.Split(':');

		var lt = CellRefs.Parse(dimSplit[0]);
		var br = CellRefs.Parse(dimSplit[1]);

		wb.RowCount = Math.Max(lt.row, br.row) + 1;
		wb.ColumnCount = Math.Max(lt.column, br.column) + 1;

		var mg = workSheetPart.Worksheet.GetFirstChild<PageMargins>();
		if (mg != null)
		{
			// in INCHES
			var lm = mg.Left?.Value * 72F ?? 0;
			var rm = mg.Right?.Value * 72F ?? 0;
			var tm = mg.Top?.Value * 72F ?? 0;
			var bm = mg.Bottom?.Value * 72F ?? 0;
			FormattableString fs = $"{tm},{rm},{bm},{lm}";
			ws.Margin = Thickness.FromString(fs.ToString(CultureInfo.InvariantCulture));
		}

		var ps = workSheetPart.Worksheet.GetFirstChild<PageSetup>();
		if (ps != null && ps.Orientation != null)
		{
			if (ps.Orientation == OrientationValues.Portrait)
				ws.Orientation = PageOrientation.Portrait;
			else if (ps.Orientation == OrientationValues.Landscape)
				ws.Orientation = PageOrientation.Landscape;
		}

		var hf = workSheetPart.Worksheet.GetFirstChild<HeaderFooter>();
		if (hf != null)
		{
			var pageFooter = hf.OddFooter?.InnerText;
			ws.Workbook.PageFooter = XPageFooter.FromString(pageFooter);
		}

		var defNames = workBook?.DefinedNames?.Elements<DefinedName>();

		if (defNames != null)
		{
			foreach (var defName in defNames)
			{
				var df = CreateRange(defName);
				if (df != null)
				{
					if (df.Value == "{Header}")
						wb.Header = df;
					else if (df.Value == "{Footer}")
						wb.Footer = df;
					else if (df.Value == "{TableHeader}")
						wb.TableHeader = df;
					else if (df.Value == "{TableFooter}")
						wb.TableFooter = df;
					else
						wb.Ranges.Add(df);
				}
				else
					_fitColumn = GetFitRange(defName);
			}
		}

		var rows = workSheetPart.Worksheet.Descendants<Row>()
			?? throw new InvalidOperationException($"The sheet does not have a rows");

		var defRowHeight = workSheetPart.Worksheet.SheetFormatProperties?.DefaultRowHeight;
		HashSet<String> _cellStyles = [];
		foreach (var row in rows)
		{
			var ix = row.RowIndex ?? 0;
			var wbrow = CreateRow(row, defRowHeight);
			if (wbrow != null)
				wb.Rows.Add(ix, wbrow);
			foreach (var cell in row.Elements<Cell>()) {
				var cellRef = cell.CellReference?.ToString()
						?? throw new InvalidOperationException($"CellRef is null");
				var xc = CreateCell(cell, sharedStringTable);
				if (xc != null)
				{
					wb.Cells.Add(cellRef, xc);
					if (xc.Style != null)
						_cellStyles.Add(xc.Style);
					var realCellRef = CellRefs.Parse(cellRef);
					_realColumns = Math.Max(_realColumns, realCellRef.column + 1);
					_realRows = Math.Max(_realRows, realCellRef.row + 1);
				}
			}
		}

		var columns = workSheetPart.Worksheet.Descendants<Column>()
			?? throw new InvalidOperationException($"The sheet does not have a columns");
		foreach (var col in columns)
		{
			if (col.Min != null && col.Max != null && col.Min.Value == col.Max.Value)
			{
				var colRef = CellRefs.Index2Col(col.Min.Value - 1);
				var xc = CreateColumn(col, colRef == _fitColumn);
				if (xc != null)
				{
					wb.Columns.Add(colRef, xc);
				}
			}
		}

		var mrgCelss = workSheetPart.Worksheet.Descendants<MergeCell>();
		foreach (var mc in mrgCelss)
		{
			if (mc.Reference == null)
				continue;
			var rxSplit = mc.Reference.ToString()?.Split(':');
			if (rxSplit == null)
				continue;

			var (row1, column1) = CellRefs.Parse(rxSplit[0]);
			var (row2, column2) = CellRefs.Parse(rxSplit[1]);

			if (wb.Cells.TryGetValue(rxSplit[0], out XCell? xCell) && xCell != null)
			{
				var cs = column2 - column1 + 1;
				if (cs > 1)
					xCell.ColSpan = cs;
				var rs = row2 - row1 + 1;
				if (rs > 1)
					xCell.RowSpan = rs;
			}
		}

        if (styleSheet != null)
		{
			var refs = new StyleRefs(styleSheet);
			var formats = styleSheet.Descendants<CellFormat>().ToArray();
			for (var i = 1; i < formats.Length; i++)
			{
				var cf = formats[i];
				var styleRef = $"S{i - 1}";
				var rs = CreateStyle(cf, refs);
				if (rs != null && _cellStyles.Contains(styleRef))
					wb.Styles.Add(styleRef, rs);	
			}
			ws.FontFamily = refs.DefaultFontFamily;
			ws.FontSize = refs.DefaultFontSize;
		}

		FitColumnCount(ws.Workbook);
		FixRowCount(ws.Workbook);

		return ws;
	}

	void FixRowCount(XWorkbook wb)
	{
		if (wb.RowCount > _realRows)
			wb.RowCount = _realRows;
	}

	void FitColumnCount(XWorkbook wb)
	{
		if (wb.ColumnCount > _realColumns)
			wb.ColumnCount = _realColumns;
	}

	static String? GetFitRange(DefinedName dn)
	{
		if (dn == null)
			return null;
		String? name = dn.Name;
		if (name == null || name != "Fit")
			return null;
		String showRef = dn.Text;
		Int32 exclPos = showRef.IndexOf('!');
		if (exclPos == -1)
			return null;
		// String shtName = showRef[..exclPos];
		String shtRef = showRef[(exclPos + 1)..];
		Int32 colonPos = shtRef.IndexOf(':');
		if (colonPos == -1)
			return null;
		string startRef = shtRef[..colonPos]; // link to the first line of the range
		String endRef = shtRef[(colonPos + 1)..];  // link to the second line of the range
		if (startRef.Length < 2)
			return null;
		if (endRef.Length < 2)
			return null;
		String? startCol = null;
		String? endCol = null;
		if (startRef[0] == '$')
		{
			startCol = startRef[1..];
		}
		if (endRef[0] == '$')
		{
			endCol = endRef[1..];
		}
		return startCol == endCol ? startCol : null;
	}

	static XRange? CreateRange(DefinedName dn)
	{
		if (dn == null)
			return null;
		String? name = dn.Name;
		if (name == null)
			return null;
		String showRef = dn.Text;
		Int32 exclPos = showRef.IndexOf('!');
		if (exclPos == -1)
			return null;
		// String shtName = showRef[..exclPos];
		String shtRef = showRef[(exclPos + 1)..];
		Int32 colonPos = shtRef.IndexOf(':');
		if (colonPos == -1)
			return null;
		string startRef = shtRef[..colonPos]; // link to the first line of the range
		String endRef = shtRef[(colonPos + 1)..];  // link to the second line of the range
		if (startRef.Length < 2)
			return null;
		if (endRef.Length < 2)
			return null;
		UInt32 startRow = 0;
		UInt32 endRow = 0;
		if (startRef[0] == '$')
		{
			if (!UInt32.TryParse(startRef[1..], out startRow))
				return null;
		}
		if (endRef[0] == '$')
		{
			if (!UInt32.TryParse(endRef[1..], out endRow))
				return null;
		}
		var rng = new XRange() {
			Value = $"{{{name.Replace('_', '.')}}}",
			Start = startRow,
			End = endRow
		};
		return rng;
	}

	static XCell? CreateCell(Cell cell, SharedStringTable sharedStringTable)
	{
		String? style = null;
		if (cell.DataType == null && cell.CellValue == null && cell.StyleIndex == null)
			return null;
		if (cell.StyleIndex != null)
			style = $"S{cell.StyleIndex.Value}";
		var xcell = new XCell()
		{
			Style = style,
		};
		if (cell.DataType == null || cell.CellValue == null)
			return xcell;
		if (cell.DataType == CellValues.SharedString)
		{
			Int32 ssid = Int32.Parse(cell.CellValue.Text);
			String str = sharedStringTable.ChildElements[ssid].InnerText;
			xcell.Value = str;
		}
		return xcell;
	}

	static XColumn? CreateColumn(Column? col, Boolean fit)
	{
		if (col == null) 
			return null;
		Single ptW = 0;
		if (col.CustomWidth != null)
		{
			if (col.CustomWidth.Value && col.Width != null)
			{
				var fw = col.Width.Value;
				var pxW = Math.Truncate(((256 * fw + Math.Truncate(128 / CHAR_WIDTH)) / 256) * CHAR_WIDTH);
				ptW = (Single) pxW * 72F / 96F;
			}
		}
		return new XColumn() { Width = fit ? -1 : ptW };
	}

	static XRow? CreateRow(Row? row, DoubleValue? defRowHeight)
	{
		if (row == null)
			return null;
		var h = row.Height; // pt
		if (h == null)
			return null;
		if (defRowHeight != null && h.Value == defRowHeight.Value)
			return null;
		return new XRow()
		{
			Height = (Single)h.Value
		};
	}

	static XStyle? CreateStyle(CellFormat? cf, StyleRefs refs)
	{
		if (cf == null)
			return null;
		String? background = null;
		Single? fontSize = null;
		Boolean? fontBold = null;
		Boolean? fontItalic = null;
		Boolean? fontUnderline = null;
		String? fontName = null;
		Thickness? border = null;
		TextAlign? align = null;
		VertAlign? vAlign = null;
		String? format = null;
		UInt32? textRotation = null;

		if (cf?.ApplyFill?.Value == true)
			background = refs.GetFill(cf.FillId?.Value);

		if (cf?.ApplyBorder?.Value == true)
			border = refs.GetBorder(cf.BorderId?.Value);

		if (cf?.ApplyFont?.Value == true)
		{
			var ff = refs.GetFont(cf.FontId?.Value);
			if (ff != null)
			{
				var fv = ff.Value;
				fontSize = fv.fontSize;
				fontBold = fv.bold;
				fontItalic = fv.italic;	
				fontUnderline = fv.underline;	
				fontName = fv.name;
			}
		}

		if (cf?.ApplyNumberFormat?.Value  == true)
		{
			format = refs.GetFormat(cf.NumberFormatId);
		}

		if (cf?.ApplyAlignment?.Value == true)
		{
			var a = cf.Alignment;
			if (a?.Horizontal != null)
			{
				if (a.Horizontal == HorizontalAlignmentValues.Right)
					align = TextAlign.Right;
				else if (a.Horizontal == HorizontalAlignmentValues.Center)
					align = TextAlign.Center;
				else if (a.Horizontal == HorizontalAlignmentValues.Justify)
					align = TextAlign.Justify;
			}
			if (a?.Vertical != null)
			{
				if (a.Vertical == VerticalAlignmentValues.Top)
					vAlign = VertAlign.Top;
				else if (a.Vertical == VerticalAlignmentValues.Center)
					vAlign = VertAlign.Middle;
			}
			textRotation = a?.TextRotation?.Value;
		}

		if (background == null && fontName == null && fontSize == null && border == null && align == null && 
			vAlign == null && fontBold == null && fontItalic == null && fontUnderline == null &&
			format == null && textRotation == null)
			return null;

		return new XStyle()
		{
			FontName = fontName,
			FontSize = fontSize,	
			Bold = fontBold,
			Italic = fontItalic,
			Underline = fontUnderline,
			Background = background,
			Border = border,
			Align = align,
			VAlign = vAlign,
			Format = format,
			TextRotation = textRotation
		};
	}
}



using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;


using XWorkbook = A2v10.Xaml.Report.Spreadsheet.Workbook;
using XCell = A2v10.Xaml.Report.Spreadsheet.Cell;
using XRow = A2v10.Xaml.Report.Spreadsheet.Row;
using XColumn = A2v10.Xaml.Report.Spreadsheet.Column;
using A2v10.ReportEngine.Pdf;

namespace ReportEngineExcel;

internal class ExcelConvertor
{
	private readonly String _fileName;
	public ExcelConvertor(String fileName)
	{
		_fileName = fileName;
	}

	public XWorkbook ParseFile()
	{

		var wb = new XWorkbook();

		var doc = SpreadsheetDocument.Open(_fileName, isEditable: false);
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
		// This formats is NUMBER, not standard!
		var numFormats = stylesPart?.Stylesheet
			.Descendants<NumberingFormat>()?
			.GroupBy(x => x.NumberFormatId?.Value.ToString() ?? String.Empty)
			.ToDictionary(g => g.Key, g => g.First());

		var dim = workSheetPart.Worksheet.SheetDimension?.Reference?.ToString()
			?? throw new InvalidOperationException("Invalid SheetDimension");
		var dimSplit = dim.Split(':');

		var lt = CellRefs.Parse(dimSplit[0]);
		var br = CellRefs.Parse(dimSplit[1]);

		wb.RowCount = Math.Max(lt.row, br.row) + 1;
		wb.ColumnCount = Math.Max(lt.column, br.column) + 1;

		var rows = workSheetPart.Worksheet.Descendants<Row>()
			?? throw new InvalidOperationException($"The sheet does not have a rows");
		foreach (var row in rows)
		{
			var ix = row.RowIndex ?? 0;
			var wbrow = CreateRow(row);
			if (wbrow != null)
				wb.Rows.Add(ix, wbrow);
			foreach (var cell in row.Elements<Cell>()) {
				var cellRef = cell.CellReference?.ToString()
						?? throw new InvalidOperationException($"CellRef is null");
				var xc = CreateCell(cell, sharedStringTable);
				if (xc != null)
					wb.Cells.Add(cellRef, xc);
			}
		}

		var columns = workSheetPart.Worksheet.Descendants<Column>()
			?? throw new InvalidOperationException($"The sheet does not have a columns");
		foreach (var col in columns)
		{
			var xc = CreateColumn(col);
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

			Console.WriteLine(mc.Reference?.ToString());
		}
		return wb;
	}

	XCell? CreateCell(Cell cell, SharedStringTable sharedStringTable)
	{
		if (cell.DataType == null || cell.CellValue == null)
			return null;
		if (cell.DataType == CellValues.SharedString)
		{
			Int32 ssid = Int32.Parse(cell.CellValue.Text);
			String str = sharedStringTable.ChildElements[ssid].InnerText;
			return new XCell() { Value = str };
		}
		return null;
	}

	XColumn? CreateColumn(Column? col)
	{
		if (col == null) 
			return null;
		return new XColumn();
	}

	XRow? CreateRow(Row? row)
	{
		if (row == null)	
			return null;
		return new XRow();
	}
}

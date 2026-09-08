// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Xaml.Report.Spreadsheet;
using A2v10.ReportEngine.Script;
using A2v10.ReportEngine.Excel;

using XRange = A2v10.Xaml.Report.Spreadsheet.Range;

namespace Test.PdfReportEngine;

/*
Развёртка книги в матрицу: строка без диапазона считается от корня, строка внутри диапазона —
от его элемента, вложенный диапазон — от элемента внешнего. Ни PDF, ни файла Excel: матрица
строится в конструкторе WorkbookHelper и содержит готовые строки.

Лист (1-based):
  1              {Document.No}                      корень
  2   диапазон   {Document.Rows}   A: {Name}   B: {(rowTotalArrow(this))}
  3-4 диапазон   {Document.Rows}   A: {Name}
  4   диапазон   {Marks}           A: {Code}   вложен в предыдущий
*/

[TestClass]
[TestCategory("Workbook Scope")]
public class WorkbookScope
{
	private readonly WorkbookHelper _helper;

	const String CODE = "const rowTotalArrow = (row) => row.Price * row.Qty;";

	public WorkbookScope()
	{
		var model = CreateModel();
		var context = new RenderContext(String.Empty, new TestLocalizer(), model, CODE);
		_helper = new WorkbookHelper(CreateWorkbook(), context);
	}

	static ExpandoObject CreateModel()
	{
		static ExpandoObject Mark(String code)
		{
			var m = new ExpandoObject();
			m.Set("Code", code);
			return m;
		}

		static ExpandoObject Row(String name, Decimal price, Decimal qty)
		{
			var r = new ExpandoObject();
			r.Set("Name", name);
			r.Set("Price", price);
			r.Set("Qty", qty);
			r.Set("Marks", new List<ExpandoObject>() { Mark($"{name}-1"), Mark($"{name}-2") });
			return r;
		}

		var doc = new ExpandoObject();
		doc.Set("No", "A-1");
		doc.Set("Rows", new List<ExpandoObject>() { Row("Ten", 10M, 2M), Row("Twenty", 20M, 3M) });

		var root = new ExpandoObject();
		root.Set("Document", doc);
		return root;
	}

	static Workbook CreateWorkbook()
	{
		var wb = new Workbook()
		{
			RowCount = 4,
			ColumnCount = 2
		};
		wb.Cells.Add("A1", new Cell() { Value = "{Document.No}" });
		wb.Cells.Add("A2", new Cell() { Value = "{Name}" });
		wb.Cells.Add("B2", new Cell() { Value = "{(rowTotalArrow(this))}" });
		wb.Cells.Add("A3", new Cell() { Value = "{Name}" });
		wb.Cells.Add("A4", new Cell() { Value = "{Code}" });

		wb.Ranges.Add(new XRange() { Value = "{Document.Rows}", Start = 2, End = 2 });
		wb.Ranges.Add(new XRange() { Value = "{Document.Rows}", Start = 3, End = 4 });
		wb.Ranges.Add(new XRange() { Value = "{Marks}", Start = 4, End = 4 });
		return wb;
	}

	String? Cell(Int32 row, Int32 column)
	{
		return _helper.CellMatrix?[row, column]?.Value;
	}

	[TestMethod]
	public void RowWithoutRangeReadsRoot()
	{
		Assert.AreEqual("A-1", Cell(0, 0));
	}

	[TestMethod]
	public void RangeRowReadsItsElement()
	{
		Assert.AreEqual("Ten", Cell(1, 0));
		Assert.AreEqual("Twenty", Cell(2, 0));
	}

	[TestMethod]
	public void ScriptInRangeRowSeesElementAsThis()
	{
		Assert.AreEqual("20", Cell(1, 1));
		Assert.AreEqual("60", Cell(2, 1));
	}

	[TestMethod]
	public void NestedRangeReadsOuterElement()
	{
		// два уровня — предел плоской развёртки строк (два вложенных цикла в GetRealRows),
		// третьему лечь некуда; scope тут ни при чём
		Assert.AreEqual("Ten", Cell(3, 0));
		Assert.AreEqual("Ten-1", Cell(4, 0));
		Assert.AreEqual("Ten-2", Cell(5, 0));
		Assert.AreEqual("Twenty", Cell(6, 0));
		Assert.AreEqual("Twenty-1", Cell(7, 0));
		Assert.AreEqual("Twenty-2", Cell(8, 0));
	}
}

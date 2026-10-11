// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Xaml.Report.Spreadsheet;
using A2v10.ReportEngine.Script;
using A2v10.ReportEngine.Excel;

using XRange = A2v10.Xaml.Report.Spreadsheet.Range;

namespace Test.PdfReportEngine;

/*
Диапазон над путём, которого нет в модели: пустой диапазон, и развёртка идёт дальше.
Раньше GetRealRows делал continue мимо r++ и находил тот же диапазон снова - конструктор не возвращался.

  1              {Document.No}
  2   диапазон   {Document.Missing}   A: {Name}
  3              {Document.No}
*/

[TestClass]
[TestCategory("Workbook Scope")]
public class WorkbookMissingRange
{
	static WorkbookHelper Build()
	{
		var doc = new ExpandoObject();
		doc.Set("No", "A-1");
		var root = new ExpandoObject();
		root.Set("Document", doc);

		var wb = new Workbook() { RowCount = 3, ColumnCount = 1 };
		wb.Cells.Add("A1", new Cell() { Value = "{Document.No}" });
		wb.Cells.Add("A2", new Cell() { Value = "{Name}" });
		wb.Cells.Add("A3", new Cell() { Value = "{Document.No}" });
		wb.Ranges.Add(new XRange() { Value = "{Document.Missing}", Start = 2, End = 2 });

		var context = new RenderContext(new TestCodeProvider(), String.Empty, new TestLocalizer(), root, String.Empty);
		return new WorkbookHelper(wb, context);
	}

	[TestMethod]
	public void MissingCollectionIsAnEmptyRange()
	{
		var build = Task.Run(Build);
		Assert.IsTrue(build.Wait(TimeSpan.FromSeconds(10)), "the row walk did not end");

		var matrix = build.Result.CellMatrix;
		Assert.AreEqual("A-1", matrix?[0, 0]?.Value);
		Assert.AreEqual("A-1", matrix?[1, 0]?.Value);
	}
}

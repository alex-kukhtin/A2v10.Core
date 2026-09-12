// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Xaml.Report;

namespace Test.PdfReportEngine;

/*
Оформление элемента складывается из двух слоёв: стиль из мешка по селектору, а поверх него —
свойства, написанные на самом элементе (ApplyStylesSelf). Второй слой легко забыть, и тогда
свойство молча ничего не делает: у Column ключа в мешке нет вовсе, поэтому Margin на ней
терялся целиком.
*/

[TestClass]
[TestCategory("Element Styles")]
public class ElementStyles
{
	// Margin на Column: в мешке ключа ">Column" нет, так что весь стиль приходит от элемента
	[TestMethod]
	public void ColumnKeepsItsOwnDecoration()
	{
		var column = new Column()
		{
			Margin = Thickness.FromString("0,0,10pt,0"),
			Border = Thickness.FromString(".2pt"),
			Bold = true
		};
		var page = new Page();
		page.Columns.Add(column);
		page.ApplyStyles("Root", new StyleBag());

		var rs = column.RuntimeStyle;
		Assert.IsNotNull(rs);
		Assert.AreEqual(10F, rs.Margin?.Bottom.Value);
		Assert.AreEqual(0F, rs.Margin?.Top.Value);
		Assert.AreEqual(0.2F, rs.Border?.Top.Value);
		Assert.AreEqual(true, rs.Bold);
	}

	// Два слоя вместе: рамка и отступы приходят из мешка, заливка — с самой ячейки
	[TestMethod]
	public void OwnDecorationWinsOverTheBag()
	{
		var cell = new TableCell() { Background = "#ffffff" };
		var row = new TableRow();
		row.Cells.Add(cell);
		var table = new Table() { Style = TableStyle.Details };
		table.Header.Add(row);
		table.ApplyStyles("Root", new StyleBag());

		var rs = cell.RuntimeStyle;
		Assert.IsNotNull(rs);
		Assert.AreEqual("#ffffff", rs.Background);
		Assert.AreEqual(0.2F, rs.Border?.Top.Value);
		Assert.AreEqual(4F, rs.Padding?.Left.Value);
	}
}

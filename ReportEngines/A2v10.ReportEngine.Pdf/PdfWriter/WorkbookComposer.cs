// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report.Spreadsheet;

namespace A2v10.ReportEngine.Pdf;

internal class WorkbookComposer(Workbook _workbook, RenderContext _context) : FlowElementComposer
{	internal override void Compose(IContainer container, Object? value = null)
	{
		if (!_context.IsVisible(_workbook))
			return;
		container
			.ApplyLayoutOptions(_workbook)
			.ApplyDecoration(_workbook.RuntimeStyle)
			.Table(ComposeTable);
	}
	public void Compose(ColumnDescriptor column)
	{
		column.Item().Element(cont =>
		{
			Compose(cont);
		});
	}

	public void ComposeTable(TableDescriptor table)
	{
		var wbh = new WorkbookHelper(_workbook, _context);
		table.ColumnsDefinition(column => {
			for (int i = 0; i < _workbook.ColumnCount; i++)
				column.ConstantColumn(wbh.ColumnWidth(i), Unit.Millimetre);
		});

		var mx = wbh.GetCellMatrix();
		for (int r=0; r <= mx.GetUpperBound(0); r++)
		{
			var rowHeight = wbh.RowHeight(r);
			for (int c =0; c <= mx.GetUpperBound(1); c++)
			{
				var wbCell = mx[r, c];
				if (wbCell != null && wbCell.IsSpanPart)
					continue;
				var tc = table.Cell().Height(rowHeight, Unit.Millimetre)
					.Border(.2F).Padding(2F);
				if (wbCell != null)
					ComposeCell(wbCell, tc);
			}
		}
	}

	private void ComposeCell(WorkbookCell wbCell, IContainer cellCont)
	{
		if (!String.IsNullOrEmpty(wbCell.Value))
			cellCont.Text(wbCell.Value);
	}

	private void ComposeCell(Int32 row, Int32 column, IContainer cellCont)
	{
		var cellRef = $"{CellRefs.Index2Col(column)}{row + 1}";
		cellCont = cellCont.Border(.2F).Padding(2F);
		if (!_workbook.Cells.TryGetValue(cellRef, out Cell? cell))
			return;
		if (cell != null && !String.IsNullOrEmpty(cell.Value))
			cellCont.Text(cell.Value);
	}
}

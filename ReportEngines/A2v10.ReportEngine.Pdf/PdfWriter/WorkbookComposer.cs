// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report.Spreadsheet;
using A2v10.ReportEngine.Script;
using A2v10.ReportEngine.Excel;

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
			for (UInt32 i = 0; i < _workbook.ColumnCount; i++)
			{
				var cw = wbh.ColumnWidth(i);
				if (cw < 0)
					column.RelativeColumn(-cw);
				else
					column.ConstantColumn(cw);
			}
		});

		var mx = wbh.CellMatrix;
		for (int r=0; r <= mx.GetUpperBound(0); r++)
		{
			var rh = wbh.RowHeight(r);
			for (int c = 0; c <= mx.GetUpperBound(1); c++)
			{
				var wbCell = mx[r, c];
				if (wbCell != null && wbCell.IsSpanPart)
					continue;
				var tc = table.Cell();
				var cellHeight = rh;
				if (wbCell?.ColSpan > 1)
					tc = tc.ColumnSpan(wbCell.ColSpan);
				if (wbCell?.RowSpan > 1)
				{
					tc = tc.RowSpan(wbCell.RowSpan);
					for (var rs=1; rs < wbCell.RowSpan; rs++)
						cellHeight += wbh.RowHeight(r + rs);
				}
				var cont = tc.MinHeight(cellHeight);
				// TODO check border
				// cont = cont.Border(.2F);
				cont = cont.ApplyDecoration(wbCell?.Cell.RuntimeStyle);
				if (wbCell != null)
					ComposeCell(wbCell, cont);
			}
		}
	}

	private static void ComposeCell(WorkbookCell wbCell, IContainer cellCont)
	{
		if (wbCell.Image != null)
			cellCont.Image(wbCell.Image.Stream);
		else if (wbCell.QrCode != null) 
			QrCodeComposer.DrawQrCode(cellCont, wbCell.QrCode.Value);
		else if (!String.IsNullOrEmpty(wbCell.Value))
			cellCont.Text(wbCell.Value)
				.LineHeight(1)
				.ApplyText(wbCell.Cell.RuntimeStyle);
	}
}

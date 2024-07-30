// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report.Spreadsheet;
using A2v10.ReportEngine.Script;
using A2v10.ReportEngine.Excel;

namespace A2v10.ReportEngine.Pdf;

internal class WorkbookComposer(Workbook _workbook, WorkbookHelper _helper, RenderContext _context) : FlowElementComposer
{	internal override void Compose(IContainer container, Object? value = null)
	{
		if (!_context.IsVisible(_workbook))
			return;
		container
			.ApplyLayoutOptions(_workbook)
			.ApplyDecoration(_workbook.RuntimeStyle)
			.Table(table => ComposeTable(table, _helper.CellMatrix, _helper.RowHeight));
	}
	public void Compose(ColumnDescriptor column)
	{
		column.Item().Element(cont =>
		{
			Compose(cont);
		});
	}

	public void ComposeFooter(ColumnDescriptor column)
	{
		column.Item().Element(cont =>
		{
			cont.Table(table => ComposeTable(table, _helper.FooterMatrix, _helper.FooterRowHeight));
		});
	}

	public void ComposePageFooter(ColumnDescriptor column)
	{
		column.Item().Element(cont =>
		{
			var center = _workbook.PageFooter?.Center;
			if (center != null)
			{
				cont.PaddingTop(5, Unit.Point).AlignCenter().Text(txt =>
				{
					foreach (var t in PageFooter.Resolve(center))
					{
						if (t == "&(Page)")
							txt.CurrentPageNumber();
						else if (t == "&(Pages)")
							txt.TotalPages();
						else
							txt.Span(t);
					}
				});
			}
		});
	}

	public void ComposeHeader(ColumnDescriptor column)
	{
		column.Item().Element(cont =>
		{
			cont.Table(table => ComposeTable(table, _helper.HeaderMatrix, _helper.HeaderRowHeight));
		});
	}

	public void ComposeTable(TableDescriptor table, WorkbookCell?[,] matrix, Func<Int32, Single> getHeight)
	{
		table.ColumnsDefinition(column => {
			for (UInt32 i = 0; i < _workbook.ColumnCount; i++)
			{
				var cw = _helper.ColumnWidth(i);
				if (cw < 0)
					column.RelativeColumn(-cw);
				else
					column.ConstantColumn(cw);
			}
		});

		for (int rn=0; rn <= matrix.GetUpperBound(0); rn++)
		{
			var rh = getHeight(rn);
			for (int c = 0; c <= matrix.GetUpperBound(1); c++)
			{
				var wbCell = matrix[rn, c];
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
						cellHeight += getHeight(rn + rs);
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
		{
			cellCont.ShowEntire().Text(wbCell.Value)
				.LineHeight(1)
				.ApplyText(wbCell.Cell.RuntimeStyle);
		}
	}
}

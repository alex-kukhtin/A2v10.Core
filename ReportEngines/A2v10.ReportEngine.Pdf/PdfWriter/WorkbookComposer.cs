// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Elements.Table;

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

	private void ComposeTable(ColumnDescriptor column)
	{
		if (!_context.IsVisible(_workbook))
			return;
		column.Item().Element(container =>
			container.ApplyLayoutOptions(_workbook)
			.ApplyDecoration(_workbook.RuntimeStyle)
			.Table(table => ComposeTable(table, _helper.TopPartMatrix, _helper.TopPartRowHeight))
		);
		column.Item().Element(container =>
			container.ApplyLayoutOptions(_workbook)
			.ApplyDecoration(_workbook.RuntimeStyle)
			.Table(table => ComposeTable(table,
				_helper.TableBodyMatrix, _helper.TableBodyRowHeight,
				_helper.TableHeaderMatrix, _helper.TableHeaderRowHeight,
				_helper.TableFooterMatrix, _helper.TableFooterRowHeight))
		);
		column.Item().Element(container =>
			container.ApplyLayoutOptions(_workbook)
			.ApplyDecoration(_workbook.RuntimeStyle)
			.Table(table => ComposeTable(table, _helper.BottomPartMatrix, _helper.BottomPartRowHeight))
		);
	}
	public void Compose(ColumnDescriptor column)
	{
		if (_helper.CellMatrix != null)
		{
			column.Item().Element(cont =>
			{
				Compose(cont);
			});
		}
		else
			ComposeTable(column);
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
		if (_helper.HeaderMatrix == null)
			return;
		column.Item().Element(cont =>
		{
			cont.Table(table => ComposeTable(table, _helper.HeaderMatrix, _helper.HeaderRowHeight));
		});
	}

	private void ComposeColumns(TableDescriptor table)
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
	}

	private static void ComposeMatrix(Func<ITableCellContainer> getCell,  WorkbookCell?[,]? matrix, Func<Int32, Single> getHeight)
	{
		if (matrix == null)
			return;
		for (int rn = 0; rn <= matrix.GetUpperBound(0); rn++)
		{
			var rh = getHeight(rn);
			for (int c = 0; c <= matrix.GetUpperBound(1); c++)
			{
				var wbCell = matrix[rn, c];
				if (wbCell != null && wbCell.IsSpanPart)
					continue;
				var tc = getCell();
				var cellHeight = rh;
				if (wbCell?.ColSpan > 1)
					tc = tc.ColumnSpan(wbCell.ColSpan);
				if (wbCell?.RowSpan > 1)
				{
					tc = tc.RowSpan(wbCell.RowSpan);
					for (var rs = 1; rs < wbCell.RowSpan; rs++)
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

	public void ComposeTable(TableDescriptor table, WorkbookCell?[,]? matrix, Func<Int32, Single> getHeight)
	{
		if (matrix == null)
			return;

		ComposeColumns(table);
		ComposeMatrix(table.Cell, matrix, getHeight);
	}

	public void ComposeTable(TableDescriptor table, 
			WorkbookCell?[,]? matrix, Func<Int32, Single> getHeight,
			WorkbookCell?[,]? header, Func<Int32, Single> getHeaderHeight,
			WorkbookCell?[,]? footer, Func<Int32, Single> getFooterHeight
		)
	{
		ComposeColumns(table);

		if (header != null)
		{
			table.Header(headCont =>
			{
				ComposeMatrix(headCont.Cell, header, getHeaderHeight);
			});
		}
		ComposeMatrix(table.Cell, matrix, getHeight);
		if (footer != null)
		{
			table.Footer(footerCont =>
			{
				ComposeMatrix(footerCont.Cell, footer, getFooterHeight);
			});
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

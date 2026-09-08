// Copyright © 2022-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Elements.Table;

using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;

namespace A2v10.ReportEngine.Pdf;

internal enum CellKind
{
	Body,
	Header,
	Footer
}

internal class TableComposer(Table table, RenderContext context) : FlowElementComposer
{
	private readonly Table _table = table;
	private readonly RenderContext _context = context;

    internal override void Compose(IContainer container, ExpandoObject scope)
	{
		if (!_context.IsVisible(_table, scope))
			return;
		container
			.ApplyLayoutOptions(_table)
			.ApplyDecoration(_table.RuntimeStyle)
			.Table(tblDescr =>
			{
				tblDescr.ColumnsDefinition(columns =>
				{
					if (_table.Columns.Count == 0)
						columns.RelativeColumn();
					else
						foreach (var cx in _table.Columns)
							columns.TableColumn(cx);
				});

				if (_table.Header.Count != 0)
					tblDescr.Header(header => ComposeHeader(header, scope));

				// ItemsSource читается в полученном scope, поэтому вложенная таблица считает свою
				// коллекцию сама, на любой глубине — родителю нечего для неё предвычислять
				var isbind = _table.GetBindRuntime("ItemsSource");
				var coll = isbind?.Expression != null
					? _context.EvaluateCollection(isbind.Expression, scope)
					: null;
				if (coll != null)
					foreach (var elem in coll)
						ComposeRowCollection(CellKind.Body, tblDescr, _table.Body, elem);
				else
					ComposeRowCollection(CellKind.Body, tblDescr, _table.Body, scope);

				// not footer! inside body
				ComposeRowCollection(CellKind.Footer, tblDescr, _table.Footer, scope);
			});
	}

	void ComposeHeader(TableCellDescriptor header, ExpandoObject scope)
	{
		foreach (var cell in _table.Header.Cells())
			ComposeCell(CellKind.Header, cell, () => header.Cell(), scope);
	}

	private void ComposeCell(CellKind _1/*kind*/, TableCell cell, Func<ITableCellContainer> createCell, ExpandoObject scope)
	{
		if (!_context.IsVisible(cell, scope))
			return;
		var cellCont = createCell();
		if (cell.RowSpan > 1)
			cellCont = cellCont.RowSpan(cell.RowSpan);
		if (cell.ColSpan > 1)
			cellCont = cellCont.ColumnSpan(cell.ColSpan);

		var ci = cellCont.ApplyCellDecoration(cell.RuntimeStyle);

		if (cell.Content is FlowElement flowElem)
		{
			flowElem.CreateComposer(_context).Compose(ci, scope);
			return;
		}

		var val = _context.GetValueAsString(cell, scope);
		if (val != null)
			ci.Text(val).ApplyText(cell.RuntimeStyle);
	}

	private void ComposeRowCollection(CellKind kind, TableDescriptor tbl, TableRowCollection body, ExpandoObject scope)
	{
		foreach (var row in body)
		{
			if (!_context.IsVisible(row, scope))
				continue;
			foreach (var cell in row.Cells)
				ComposeCell(kind, cell, () => tbl.Cell(), scope);
		}
	}
}

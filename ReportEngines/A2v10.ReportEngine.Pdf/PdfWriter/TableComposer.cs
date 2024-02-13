// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Collections.Generic;

using Jint.Native;

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

	private readonly Dictionary<TableCell, JsValue> _accessFuncs = [];

    internal override void Compose(IContainer container, Object? value = null)
	{
		if (!_context.IsVisible(_table))
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
					tblDescr.Header(ComposeHeader);

				CreateAccessFunc(_table.Body);
				IList<ExpandoObject>? coll = null;
				if (value != null && value is IList<ExpandoObject> list)
					coll = list;
				else
				{
					var isbind = _table.GetBindRuntime("ItemsSource");
					if (isbind != null && isbind.Expression != null)
					{
						coll = _context.Engine.EvaluateCollection(isbind.Expression);
					}
				}
				if (coll != null)
					foreach (var elem in coll)
						ComposeRowCollection(CellKind.Body, tblDescr, _table.Body, elem);
				else
					ComposeRowCollection(CellKind.Body, tblDescr, _table.Body);

				// not footer! inside body
				ComposeRowCollection(CellKind.Footer, tblDescr, _table.Footer);
			});
	}


	void ComposeHeader(TableCellDescriptor header)
	{
		foreach (var cell in _table.Header.Cells())
			ComposeCell(CellKind.Header, cell, () => header.Cell());
	}

	private void CreateAccessFunc(TableRowCollection body)
	{
		foreach (var row in body)
		{
			foreach (var cell in row.Cells)
			{
				var cont = cell.GetBindRuntime("Content");
				if (cont != null && cont.Expression != null)
				{
					var func = _context.Engine.CreateAccessFunction(cont.Expression);
					_accessFuncs.Add(cell, func);
				}
				else if (cell.Content is FlowElement flowElem)
				{
					var isbind = flowElem.GetBindRuntime("ItemsSource");
					if (isbind != null && isbind.Expression != null)
					{
						var func = _context.Engine.CreateAccessFunction(isbind.Expression);
						_accessFuncs.Add(cell, func);
					}
				}
			}
		}
	}

	private void ComposeCell(CellKind _1/*kind*/, TableCell cell, Func<ITableCellContainer> createCell, ExpandoObject? data = null)
	{
		if (!_context.IsVisible(cell))
			return;
		var cellCont = createCell();
		if (cell.RowSpan > 1)
			cellCont = cellCont.RowSpan(cell.RowSpan);
		if (cell.ColSpan > 1)
			cellCont = cellCont.ColumnSpan(cell.ColSpan);

		DataType cellDataType = DataType.String;
		String? cellFormat = null;
		var bind = cell.GetBindRuntime("Content");
		if (bind != null)
		{
			cellDataType = bind.DataType;
			cellFormat = bind.Format;
		}

		var ci = cellCont.ApplyCellDecoration(cell.RuntimeStyle);

		if (!_context.IsVisible(cell))
			return;

		// TODO: style here
		// var ci = cellCont.Background("#f5f5f5").Border(.2F).Padding(2F);

		if (_accessFuncs.TryGetValue(cell, out var contentFunc))
		{
			var value = _context.Engine.Invoke(contentFunc, data, bind?.Expression);
			if (cell.Content is FlowElement nestedFlow)
				nestedFlow.CreateComposer(_context).Compose(ci, value);
			else if (value != null)
				ci.Text(_context.ValueToString(value, cellDataType, cellFormat)).ApplyText(cell.RuntimeStyle);
			return;
		}

		if (cell.Content is FlowElement flowElem)
			flowElem.CreateComposer(_context).Compose(ci, data);
		else
		{
			var val = _context.GetValueAsString(cell);
			if (val != null)
			{
				ci.Text(val).ApplyText(cell.RuntimeStyle);
			}
		}
	}

	private void ComposeRowCollection(CellKind kind, TableDescriptor tbl, TableRowCollection body, ExpandoObject? data = null)
	{
		foreach (var row in body)
		{
			if (!_context.IsVisible(row))
				continue;
			foreach (var cell in row.Cells)
				ComposeCell(kind, cell, () => tbl.Cell(), data);
		}
	}
}

// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

using Jint.Native;

using A2v10.Xaml.Report.Spreadsheet;

namespace A2v10.ReportEngine.Pdf;

/*
 * TODO: 
 * 7. Page.Title - {}
 */
internal record WorkbookCell
{
	public WorkbookCell(Cell cell)
	{
		Cell = cell;
		Value = cell.Value;
	}
	public WorkbookCell(Boolean isSpanPart)
	{
		IsSpanPart = isSpanPart;
		Cell = new Cell();
	}

	public Cell Cell {  get; init; }
	public UInt32 ColSpan => Cell.ColSpan;
	public UInt32 RowSpan => Cell.RowSpan;
	public Boolean IsSpanPart { get; init; }	
	public String? Value { get; set;}
}

public record RealRow(UInt32 CellRow, ExpandoObject? Item = null);
internal class WorkbookHelper
{
	// 1pt = 1/72in, 1in = 96px

	private const Single DEFAULT_COLUMN_WIDTH = 54; // pt
	private const Single DEFAULT_ROW_HEIGHT = 15; // pt

	private readonly Workbook _workbook;
	private readonly RenderContext _context;

	private readonly WorkbookCell?[,] _matrix;
	private readonly List<RealRow> _realRows;

	public WorkbookCell?[,] CellMatrix => _matrix;

	public WorkbookHelper(Workbook workbook, RenderContext context)
	{
		_workbook = workbook;
		_context = context;
		(_matrix, _realRows) = CreateCellMatrix();
	}

	private IEnumerable<RealRow> GetRealRows()
	{
		var count  = _workbook.RowCount;	
		UInt32 r = 0;
		while (r < count)
		{
			var rng = _workbook.Ranges.Find(rng => rng.Start == r + 1);
			if (rng == null)
				yield return new RealRow(r + 1);
			else
			{
				var coll = _context.Engine.EvaluateCollection(rng.Value[1..^1]);
				if (coll == null)
					continue;
				foreach (var colElem in coll)
				{
					for (UInt32 k=rng.Start; k<=rng.End; k++)
						yield return new RealRow(k, colElem);
				}
				r += rng.End - rng.Start;
			}
			r++;
		}
	}

	private WorkbookCell CreateWorkbookCell(Cell cell, ExpandoObject? item, JsValue? accessFunc)
	{
		var wbCell = new WorkbookCell(cell);
		var val = cell.Value;
		if (String.IsNullOrEmpty(val))
			return wbCell;
		val = val.Trim();
		if (item != null && accessFunc != null)
			wbCell.Value = _context.ValueToString(_context.Engine.Invoke(accessFunc, item, null),
				cell.DataType, cell.Format);
        else if (val.StartsWith('{') && val.EndsWith('}')) {
			wbCell.Value = _context.ValueToString(_context.Engine.EvaluateValue(val[1..^1]),
				cell.DataType, cell.Format);
		}
        return wbCell;
	}

	private (WorkbookCell?[,] mx, List<RealRow> rows) CreateCellMatrix()
	{
		var realRows = GetRealRows().ToList();
		var mx = new WorkbookCell[realRows.Count, _workbook.ColumnCount];
		for (var r=0; r<realRows.Count; r++)
		{
			var rr = realRows[r];
			for (var c = 0; c < _workbook.ColumnCount; c++)
			{
				var cellRef = $"{CellRefs.Index2Col(c)}{rr.CellRow}";
				if (_workbook.Cells.TryGetValue(cellRef, out var wbCell))
				{
					JsValue? accessFunc = null;
					if (rr.Item != null)
						accessFunc = _context.Engine.CreateAccessFunctionBracess(wbCell.Value);
					mx[r, c] = CreateWorkbookCell(wbCell, rr.Item, accessFunc);
					if (wbCell.ColSpan > 1 && wbCell.RowSpan > 1)
					{
						for (var cj = c + 1; cj < c + wbCell.ColSpan; cj++)
							mx[r, cj] = new WorkbookCell(true); // right
						for (var rj = 1;  rj < wbCell.RowSpan; rj++)
						{
							for (var cj = c; cj < c + wbCell.ColSpan; cj++)
								mx[r + rj, cj] = new WorkbookCell(true);
						}
					}
					else if (wbCell.ColSpan > 1)
						for (var cj = c + 1; cj < c + wbCell.ColSpan; cj++)
							mx[r, cj] = new WorkbookCell(true);
					else if (wbCell.RowSpan > 1)
						for (var rj = r + 1; rj < r + wbCell.RowSpan; rj++)
							mx[rj, c] = new WorkbookCell(true);
				}
			}
		}
		return (mx, realRows);
	}
	public Single ColumnWidth(Int32 column)
	{
		var width = DEFAULT_COLUMN_WIDTH;
		if (_workbook.Columns.TryGetValue(CellRefs.Index2Col(column), out var sheetColumn))
			width = sheetColumn.Width;
		return width;
	}

	public Single RowHeight(Int32 r)
	{
		var rowHeight = DEFAULT_ROW_HEIGHT;
		var row = _realRows[r].CellRow;
		if (_workbook.Rows.TryGetValue(row, out var sheetRow))
			rowHeight = sheetRow.Height;
		return rowHeight;
	}
}

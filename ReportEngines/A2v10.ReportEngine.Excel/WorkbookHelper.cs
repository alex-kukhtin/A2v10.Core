// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Dynamic;
using System.Collections.Generic;

using A2v10.Xaml.Report.Spreadsheet;
using A2v10.ReportEngine.Script;


namespace A2v10.ReportEngine.Excel;

public record CellImage(Byte[] Stream);
public record CellQrCode(String Value);
public record WorkbookCell
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
	public CellImage? Image { get; set; }
	public CellQrCode? QrCode { get; set; }
}

public record RealRow(UInt32 CellRow, ExpandoObject? Item = null);
public partial class WorkbookHelper
{
	// 1pt = 1/72in, 1in = 96px

	private const Single DEFAULT_COLUMN_WIDTH = 54F; // pt
	private const Single DEFAULT_ROW_HEIGHT = 14.25F; // pt

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
					for (UInt32 k = rng.Start; k <= rng.End; k++)
					{
                        // TODO: Check nested ranges
                        var innerRng = _workbook.Ranges.Find(r => r != rng && r.Start == k);
						if (innerRng == null)
							yield return new RealRow(k, colElem);
						else
						{
							var innerColl = ScriptEngine.GetCollection(colElem, innerRng.Value[1..^1]);
							if (innerColl == null)
								continue;
							foreach (var innerElem in innerColl)
							{
								for (UInt32 m = innerRng.Start; m <= innerRng.End; m++)
								{
									yield return new RealRow(m, innerElem);
								}
							}
                        }
					}
				}
				r += rng.End - rng.Start;
			}
			r++;
		}
	}

	private WorkbookCell CreateWorkbookCell(Cell cell, ExpandoObject? item)
	{
		var wbCell = new WorkbookCell(cell);
		var val = cell.Value;
		if (String.IsNullOrEmpty(val))
			return wbCell;
		var rr = _context.Resolve(val, item ?? _context.DataModel, cell.DataType, cell.Format ?? cell.RuntimeStyle?.Format);
		if (rr != null)
		{
			wbCell.Value = rr.Value;
			if (rr.Stream != null)
				wbCell.Image = new CellImage(rr.Stream);
			else if (rr.ResultType == ResolveResultType.QrCode)
				wbCell.QrCode = new CellQrCode(rr.Value ?? String.Empty);
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
			for (UInt32 c = 0; c < _workbook.ColumnCount; c++)
			{
				var cellRef = $"{CellRefs.Index2Col(c)}{rr.CellRow}";
				if (_workbook.Cells.TryGetValue(cellRef, out var wbCell))
				{
					if (mx[r, c] == null) // may be already created with colspan
						mx[r, c] = CreateWorkbookCell(wbCell, rr.Item);
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
	public Single ColumnWidth(UInt32 column)
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

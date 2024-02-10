using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

using A2v10.Xaml.Report.Spreadsheet;

namespace A2v10.ReportEngine.Pdf;

/*
 * TODO: 
 * 1. Span
 * 2. RowHeight неправильно для Range
 * 3. 
 */
internal class WorkbookCell
{
	public WorkbookCell(Cell cell, String rf)
	{
		Value = cell.Value;
		Ref = rf;
	}
	public WorkbookCell(Boolean isSpanPart)
	{
		IsSpanPart = isSpanPart;
	}

	public Boolean IsSpanPart { get; set; }	
	public String? Value { get; init;}
	public String? Ref { get; init;}
}

internal static class CellRefs
{
	public static String Index2Col(Int32 index)
	{
		Int32 q = index / 26;

		if (q > 0)
			return Index2Col(q - 1) + (Char)((Int32)'A' + (index % 26));
		else
			return String.Empty + (Char)((Int32)'A' + index);
	}

	public static (Int32 row, Int32 column) Parse(String refs)
	{
		Int32 ci = 0;
		Int32 ri = 0;
		refs = refs.ToUpper();
		for (Int32 ix = 0; ix < refs.Length; ix++)
		{
			if (refs[ix] >= 'A')
				ci = (ci * 26) + ((Int32)refs[ix] - 64);
			else
			{
				ri = Int32.Parse(refs[ix..]) - 1;
				break;
			}
		}
		return (ri, ci - 1);
	}
}

public record RealRow(Int32 CellRow, ExpandoObject? item = null);
internal class WorkbookHelper
{
	private const Int32 DEFAULT_COLUMN_WIDTH = 10; // mm
	private const Int32 DEFAULT_ROW_HEIGHT = 7; // mm

	private readonly Workbook _workbook;
	private readonly RenderContext _context;

	public WorkbookHelper(Workbook workbook, RenderContext context)
	{
		_workbook = workbook;
		_context = context;
	}

	private IEnumerable<RealRow> GetRealRows()
	{
		var count  = _workbook.RowCount;	
		Int32 r = 0;
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
					for (int k=rng.Start; k<=rng.End; k++)
						yield return new RealRow(k, colElem);
				}
				r += rng.End - rng.Start;
			}
			r++;
		}
	}
	public WorkbookCell?[,] GetCellMatrix()
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
					mx[r, c] = new WorkbookCell(wbCell, cellRef);
				}
			}
		}
		return mx;
	}
	public Int32 ColumnWidth(Int32 column)
	{
		var width = DEFAULT_COLUMN_WIDTH;
		if (_workbook.Columns.TryGetValue(CellRefs.Index2Col(column), out var sheetColumn))
			width = sheetColumn.Width;
		return width;
	}

	public Int32 RowHeight(Int32 row)
	{
		var rowHeight = DEFAULT_ROW_HEIGHT;
		if (_workbook.Rows.TryGetValue(row + 1, out var sheetRow))
			rowHeight = sheetRow.Height;
		return rowHeight;
	}
}

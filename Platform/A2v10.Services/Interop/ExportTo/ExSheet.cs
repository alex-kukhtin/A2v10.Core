// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.


using System.Globalization;

namespace A2v10.Services.Interop;
public class ExSheet
{
	private readonly IFormatProvider _currentFormat;
	readonly List<ExRow> _body = [];
	readonly List<ExRow> _header = [];
	readonly List<ExRow> _footer = [];
	readonly List<ExRow> _headerFlat = [];
	readonly List<ExRow> _bodyFlat = [];

	public ExSheet(IFormatProvider? currentFormat = null)
	{
		_currentFormat = currentFormat ??
			CultureInfo.CreateSpecificCulture("uk-UA");
	}
	public ExSheet(String locale)
	{
		_currentFormat = CultureInfo.CreateSpecificCulture(locale);
	}

	public IList<ExColumn> Columns { get; } = new List<ExColumn>();
	public StylesDictionary Styles { get; } = new StylesDictionary();

	private List<ExRow> GetRowsByKind(RowKind kind)
	{
		return kind switch
		{
			RowKind.Header => _header,
			RowKind.Footer => _footer,
			RowKind.Body => _body,
			RowKind.HeaderFlat => _headerFlat,
			RowKind.BodyFlat => _bodyFlat,
			_ => throw new DataServiceException($"Invalid RowKind '{kind}'")
		};
	}
    public ExRow GetRow(Int32 rowNo, RowKind kind)
	{
		var rows = GetRowsByKind(kind);	
		while (rows.Count <= rowNo)
			rows.Add(new ExRow() { Kind = kind });
		return rows[rowNo];
	}

	public ExRow AddRow(RowKind kind)
	{
		var rows = GetRowsByKind(kind);
		var nrow = new ExRow() { Kind = kind };
		rows.Add(nrow);	
		return nrow;
	}

	ExCell AddSpanCell(RowKind kind, Int32 row, Int32 col)
	{
		var r = GetRow(row, kind);
		return r.SetSpanCell(col);
	}


	public ExCell AddCell(ExRow row, Object? value)
	{
		var cell = ExCell.Create(value);
		row.Cells.Add(cell);
		cell.StyleIndex = Styles.GetOrCreate(cell.GetStyle(row, String.Empty));
		return cell;
	}
	public ExCell AddCell(Int32 rowNo, ExRow exRow, CellSpan span, String value, String? dataType, String cellClass)
	{
		// first empty cell
		var row = GetRow(rowNo, exRow.Kind);
		var (cell, index) = row.AddCell();
		cell.Span = span;
		cell.SetValue(value, dataType, _currentFormat);
		cell.StyleIndex = Styles.GetOrCreate(cell.GetStyle(row, cellClass));
		if (span.Col == 0 && span.Row == 0)
			return cell;
		if (span.Col > 0 && span.Row == 0)
			for (var c = 0; c < span.Col - 1; c++)
				AddSpanCell(exRow.Kind, rowNo, index + c + 1).StyleIndex = cell.StyleIndex;
		else if (span.Col == 0 && span.Row > 0)
			for (var r = 0; r < span.Row - 1; r++)
				AddSpanCell(exRow.Kind, rowNo + r + 1, index).StyleIndex = cell.StyleIndex;
		else
		{
			// first row
			for (var c = 0; c < span.Col - 1; c++)
				AddSpanCell(exRow.Kind, rowNo, index + c + 1).StyleIndex = cell.StyleIndex;
			// next rows
			for (var r = 1; r < span.Row; r++)
			{
				for (var c = 0; c < span.Col; c++)
					AddSpanCell(exRow.Kind, rowNo + r, index + c).StyleIndex = cell.StyleIndex;
			}
		}
		return cell;
	}

	public IEnumerable<ExRow> Rows => NumerateRows();

	private IEnumerable<ExRow> NumerateRows()
	{
		foreach (var r in _header)
			yield return r;
		foreach (var r in _body)
			yield return r;
		foreach (var r in _footer)
			yield return r;
		foreach (var r in _headerFlat)
			yield return r;
		foreach (var r in _bodyFlat)
			yield return r;
	}

	public ExColumn AddColumn()
	{
		var col = new ExColumn();
		Columns.Add(col);
		return col;
	}

	public ExColumn AddColumn(UInt32 width)
	{
		var col = AddColumn();
		if (width != 0)
			col.Width = width;
		return col;
	}
}


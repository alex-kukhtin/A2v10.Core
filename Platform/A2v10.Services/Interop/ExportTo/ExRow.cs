﻿// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Services.Interop;

public enum RowKind
{
	Header,
	Footer,
	Body,
	HeaderFlat,
	BodyFlat
}

public class ExRow
{
	public RowRole Role { get; set; }
	public RowKind Kind { get; set; }
	public HorizontalAlign Align { get; set; }
	public UInt32 Height { get; set; }
	public Boolean IsGroup { get; set; }	

	public List<ExCell> Cells { get; } = [];

	public (ExCell Cell, Int32 Index) AddCell()
	{
		for (var i = 0; i < Cells.Count; i++)
		{
			var cell = Cells[i];
			if (cell.Kind == CellKind.Null)
			{
				cell.Kind = CellKind.Normal;
				return (cell, i);
			}
		}
		var newCell = new ExCell();
		Cells.Add(newCell);
		return (newCell, Cells.Count - 1);
	}

	public ExCell SetSpanCell(Int32 col)
	{
		while (Cells.Count <= col)
			Cells.Add(new ExCell() { Kind = CellKind.Null });
		ExCell cell = Cells[col];
		cell.Kind = CellKind.Span;
		return cell;
	}

	public void SetRoleAndStyle(String strClass)
	{
		var cls = Utils.ParseClasses(strClass);
		if (cls.Role != RowRole.None)
			Role = cls.Role;
		if (cls.Align != HorizontalAlign.NotSet)
			Align = cls.Align;
		IsGroup = cls.IsGroup;
	}
}

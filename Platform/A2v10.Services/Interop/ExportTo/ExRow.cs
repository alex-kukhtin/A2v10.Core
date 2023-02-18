﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace A2v10.Services.Interop.ExportTo
{
    public enum RowKind
    {
        Header,
        Footer,
        Body
    }

    public enum ExRowStyle
    {
        Default,
        Title,
        Parameter,
        LastParameter,
        Header,
        LightHeader,
        Footer,
        Total
    }

    public class ExRow
    {
        public RowRole Role { get; set; }
        public RowKind Kind { get; set; }
        public HorizontalAlign Align { get; set; }
        public UInt32 Height { get; set; }

        public IList<ExCell> Cells { get; } = new List<ExCell>();

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
        }
    }
}

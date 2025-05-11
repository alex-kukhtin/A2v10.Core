// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/*
 * Важно! Порядок добавления колонок в DataTable должен совпадать с порядком в табличном типе!
*/
internal class DataTableBuilder(TableMetadata table, AppMetadata appMeta)
{
    public DataTable BuildDataTable(ExpandoObject? data)
    {
        var dtable = CreateDataTable();
        if (data == null)
            return dtable;
        AddRow(dtable, data);
        return dtable;
    }

    public DataTable BuildDataTable(List<Object>? rows)
    {
        var dtable = CreateDataTable();

        if (rows == null || rows.Count == 0)
            return dtable;

        foreach (var src in rows)
        {
            if (src is ExpandoObject srcEo)
                AddRow(dtable, srcEo);
        }
        return dtable;
    }

    private DataTable CreateDataTable()
    {
        var dtable = new DataTable();

        foreach (var f in table.Columns.OrderBy(c => c.DbOrder))
        {
            var c = new DataColumn(f.Name, f.ClrDataType(appMeta.IdDataType));
            if (f.MaxLength != 0 && f.DataType == ColumnDataType.String)
                c.MaxLength = f.MaxLength;
            dtable.Columns.Add(c);
        }
        return dtable;
    }
    private void AddRow(DataTable dtable, ExpandoObject src)
    {
        var r = dtable.NewRow();

        dtable.Rows.Add(r);

        var columns = dtable.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (!src.HasProperty(col.ColumnName))
            {
                r[col] = DBNull.Value;
            }
            else
            { 
                var obj = src.Get<Object>(col.ColumnName);
                if (obj == null)
                    obj = DBNull.Value;
                else if (col.DataType == typeof(Guid))
                {
                    if (obj is String strObj && String.IsNullOrWhiteSpace(strObj))
                        obj = DBNull.Value;
                }
                if (obj is ExpandoObject exp)
                {
                    obj = exp.Get<Object>("Id");
                    if (obj is Int64 int64 && int64 == 0)
                        obj = DBNull.Value;
                    else if (appMeta.IdDataType == ColumnDataType.Uniqueidentifier 
                            && obj is String strVal && String.IsNullOrWhiteSpace(strVal))
                        obj = DBNull.Value;
                }
                r[col] = obj;
            }
        }
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

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

        List<DataColumn> columns = [];
        foreach (var f in table.Columns)
        {
            var c = new DataColumn(f.Name, f.ClrDataType(appMeta.IdDataType));
            if (f.MaxLength != 0)
                c.MaxLength = f.MaxLength;
            columns.Add(c);
        }
        for (int i = 0; i < columns.Count; i++)
            dtable.Columns.Add(columns[i]);
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
            if (src.HasProperty(col.ColumnName))
            {
                var obj = src.Get<Object>(col.ColumnName);
                if (obj is ExpandoObject exp)
                {
                    obj = exp.Get<Object>("Id");
                    if (obj is Int64 int64 && int64 == 0)
                        obj = DBNull.Value;
                }
                r[i] = obj;
            }
        }
    }
}

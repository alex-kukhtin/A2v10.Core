// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;

using A2v10.Infrastructure;

namespace A2v10.AppRuntimeBuilder;

internal class DataTableBuilder
{
	public static DataTable BuildDataTable(RuntimeTable table, ExpandoObject? data)
	{
		var dtable = CreateDataTable(table);
		if (data == null)
			return dtable;
        AddRow(dtable, data);
		return dtable;
	}

    public static DataTable BuildDataTable(RuntimeTable table, List<Object>? rows)
    {
        var dtable = CreateDataTable(table, skipParent:true);

        if (rows == null || rows.Count == 0)
            return dtable;

        foreach (var src in rows)
        {
            if (src is ExpandoObject srcEo)
                AddRow(dtable, srcEo);
        }
        return dtable;
    }

    private static DataTable CreateDataTable(RuntimeTable table, Boolean skipParent = false)
    {
        var dtable = new DataTable();

        List<DataColumn> columns = [];
        foreach (var f in table.RealFields().Where(f => f.Name != "Void"))
        {
            if (skipParent && f.Type == FieldType.Parent)
                continue;
            var c = new DataColumn(f.Name, f.ValueType());
            if (f.Length.HasValue)
                c.MaxLength = f.Length.Value;
            columns.Add(c);
        }
        for (int i = 0; i < columns.Count; i++)
            dtable.Columns.Add(columns[i]);
        return dtable;
    }
    private static void AddRow(DataTable dtable, ExpandoObject src)
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

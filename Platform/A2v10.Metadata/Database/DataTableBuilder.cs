using A2v10.Infrastructure;
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;

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

    private DataTable CreateDataTable(Boolean skipParent = false)
    {
        var dtable = new DataTable();

        List<DataColumn> columns = [];
        foreach (var f in table.Columns)
        {
            if (skipParent && f.Name == "Parent")
                continue;
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

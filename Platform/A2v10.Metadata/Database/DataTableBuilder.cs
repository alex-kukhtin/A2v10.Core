// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/*
 * Important! The order in which columns are added to the DataTable must match
 * their order in the table type!
*/
internal class DataTableBuilder(TableMetadata table, AppPlatformId platformId)
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

    /* A TVP of ids for Constants.SqlNames.IdTableType - one column, no shape behind it. Rows arrive
     * as whole objects (a tag carries Name and Color too), so each is reduced to its Id here, the
     * same way a reference column is reduced in AddRow.
     */
    public static DataTable BuildIdTable(List<Object>? rows, AppPlatformId platformId)
    {
        var dtable = new DataTable();
        dtable.Columns.Add(new DataColumn(Constants.FieldNames.Id, platformId.ClrType));
        foreach (var src in rows ?? [])
        {
            var id = src is ExpandoObject eo ? eo.Get<Object>(Constants.FieldNames.Id) : src;
            if (AppPlatformId.IsEmpty(id))
                continue;
            var r = dtable.NewRow();
            r[0] = id!;
            dtable.Rows.Add(r);
        }
        return dtable;
    }

    private DataTable CreateDataTable()
    {

        DataColumn CreateColumn(TableColumn f)
        {
            var c = new DataColumn(f.Name, f.ClrDataType(platformId));
            if (f.Length.HasValue && f.Type == ColumnType.String)
                c.MaxLength = f.Length.Value;
            return c;
        }

        var dtable = new DataTable();

        foreach (var f in table.AllColumns())
            dtable.Columns.Add(CreateColumn(f));
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

            var realColumnName = col.ColumnName;
            if (table.HasFolders && col.ColumnName == "Parent")
                realColumnName = "Folder";

            if (!src.HasProperty(realColumnName))
            {
                r[col] = DBNull.Value;
            }
            else
            { 
                var obj = src.Get<Object>(realColumnName);
                if (obj == null)
                    obj = DBNull.Value;
                else if (col.DataType == typeof(Guid))
                {
                    if (obj is String strObj && String.IsNullOrWhiteSpace(strObj))
                        obj = DBNull.Value;
                }
                else if (col.ColumnName == "rv")
                {
                    if (obj is String strObj)
                        obj =  (String.IsNullOrWhiteSpace(strObj)) ? DBNull.Value
                            : Convert.FromHexString(strObj);
                }
                if (obj is ExpandoObject exp)
                {
                    obj = exp.Get<Object>("Id");
                    if (AppPlatformId.IsEmpty(obj))
                        obj = DBNull.Value;
                }
                r[col] = obj;
            }
        }
    }
}

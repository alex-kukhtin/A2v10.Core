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
internal class DataTableBuilder(TableMetadata table, AppPlatformId platformId, Boolean allowEmptyStrings)
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

        foreach (var f in table.AllColumns(TableColumnPredicates.IsSentColumn))
            dtable.Columns.Add(CreateColumn(f));
        return dtable;
    }
    /* The shape is ours (the table type's columns, in its order); the value is the core's, by the one
     * conversion the classic save writes through (A2v10.Data DataModelWriter): a reference is its Id, ''
     * is NULL unless the host allows empty strings (DataConfigurationOptions.AllowEmptyStrings), rv is
     * hex, a string is parsed invariantly. A conversion of our own wrote '' where the classic one wrote
     * NULL, threw on '' in a date or a number, and parsed '12.5' in the process's culture.
     */
    private void AddRow(DataTable dtable, ExpandoObject src)
    {
        var r = dtable.NewRow();
        dtable.Rows.Add(r);
        foreach (DataColumn col in dtable.Columns)
            r[col] = src.HasProperty(col.ColumnName)
                ? A2v10.Data.SqlExtensions.ConvertTo(src.Get<Object>(col.ColumnName), col.DataType, allowEmptyStrings, col.ColumnName)
                : DBNull.Value;
    }
}

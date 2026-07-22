// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.Infrastructure;
using A2v10.Services;

namespace A2v10.Metadata;

internal static class TableColumnPredicates
{
    internal static Boolean IsIndexColumn(TableColumn col)
        => col.Type != ColumnType.RowVersion && col.Type != ColumnType.Void && col.Type != ColumnType.IsSystem;
    internal static Boolean IsEditColumn(TableColumn col)
        => col.Type != ColumnType.RowVersion && col.Type != ColumnType.Void && col.Type != ColumnType.IsSystem
            && col.Type != ColumnType.Id && col.Type != ColumnType.Done;
}

internal static class MetadataExtensions
{
    internal static EndpointKind ToEndpointKind(this String schema)
    {
        return schema switch
        {
            Constants.SchemaNames.Catalog => EndpointKind.Catalog,
            Constants.SchemaNames.Document => EndpointKind.Document,
            Constants.SchemaNames.Journal => EndpointKind.Journal,
            _ => throw new InvalidOperationException($"Invalid schema for EndpointKind {schema}")
        };
    }

    internal static String ToSqlSchema(this String folder)
    {
        return folder switch
        {
            Constants.SchemaNames.Catalog => "cat",
            Constants.SchemaNames.Document => "doc",
            Constants.SchemaNames.Journal => "jrn",
            "report" => "rep",
            "account" => "acc",
            "inforegister" => "regi",
            _ => folder
        };
    }

    internal static String EndpointPath(this TableMetadata table)
    {
        return $"/{table.Schema}/{table.Model}".ToLowerInvariant();
    }

    internal static String EndpointPathUseBase(this TableMetadata table, TableMetadata? baseTable)
    {
        if (baseTable != null)
            return baseTable.Path;
        return table.Path;
    }

    public static IPlatformUrl PlatformUrl(this TableMetadata table, String action)
    {
        var kind = action == "index" || action == "edit" && table.EditWithPage ? "_page" : "_dialog";
        var url = $"{kind}/{table.EndpointPath()}/{action}/";
        return new PlatformUrl(url);
    }

    internal static String EditEndpoint(this TableMetadata table, TableMetadata? baseTable)
    {
        var editEndpoint = $"{table.EndpointPathUseBase(baseTable)}";

        if (table.Columns.Any(c => c.Type == ColumnType.Operation))
            editEndpoint = "{Operation.Url}";

        return editEndpoint;
    }

    internal static IEnumerable<ReportItemMetadata> TypedReportItems(this TableMetadata table, ReportItemKind kind)
    {
        return table.ReportItems.Where(ri => ri.Kind == kind).OrderBy(r => r.Order);
    }

    internal static String Endpoint(this ReportItemMetadata item)
    {
        return $"/{item.RealRefSchema}/{item.RealRefTable}";
    }
    internal static String CreateField(this ReportItemMetadata item, String? prefix = null)
    {
        return $"[{prefix}{item.Column}] {item.DataType.ToSqlDataType()}";
    }

    internal static TableMetadata CreateEnumMeta(TableColumn col)
    {
        return new TableMetadata()
        {
            //Schema = col.Reference.RefSchema,
            Table = col.RefTableCheck.Table,
            /*
            Columns = [
                new TableColumn()
                    {
                        Name = "Id",
                        DataType = ColumnDataType.String,
                        MaxLength = 16,
                        Role = TableColumnRole.PrimaryKey,
                    },
                    new TableColumn()
                    {
                        Name = "Name",
                        DataType = ColumnDataType.String,
                        MaxLength = 255,
                        Role = TableColumnRole.Name,
                    }
            ]
            */
        };
    }

    internal static IEnumerable<TableColumn> AllColumns(this TableMetadata table, Func<TableColumn, Boolean>? predicate = null) =>
        table.DefaultColumns().Concat(table.Columns).Where(predicate ?? (_ => true));

    internal static IEnumerable<RefDescriptor> AllRefs(this IEnumerable<TableColumn> columns) =>
        columns.Where(c => c.IsRef || c.IsOperation).Select((c, ix) => new RefDescriptor(ix + 1, c, c.RefTable
            ?? throw new InvalidOperationException($"RefTable for {c.Name} is null")));


    internal static IEnumerable<InheritDescriptor> AllInherits(this TableMetadata table, TableMetadata? origin = null)
    {
        var declared = new Dictionary<String, InheritMetadata>(table.Inherit);
        foreach (var kp in origin?.Inherit ?? [])
            declared[kp.Key] = kp.Value;   

        if (declared.Count == 0)
            yield break;

        var columns = table.Columns; 

        static TableColumn Find(TableMetadata t, List<TableColumn> cols, String name, String what) =>
            cols.FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException($"inherit: {what} '{name}' not found in {t.SqlTableName}");

        foreach (var kp in declared)
        {
            var field = Find(table, columns, kp.Key, "field");
            var refColumn = Find(table, columns, kp.Value.Ref, "ref");
            if (!refColumn.IsRef)
                throw new InvalidOperationException($"inherit: ref '{refColumn.Name}' is not a reference");
            var refTable = refColumn.RefTableCheck;
            yield return new InheritDescriptor(field, refColumn,
                Find(refTable, refTable.Columns, kp.Value.Field, "source"));
        }
    }
    // корінь + деталі; кожна таблиця в парі зі своїм операційним контрагентом
    internal static IEnumerable<InheritDescriptor> AllInheritsDeep(this TableMetadata table, TableMetadata? origin)
    {
        foreach (var d in table.AllInherits(origin))
            yield return d;
        foreach (var detail in table.Details)
            foreach (var d in detail.Value.AllInheritsDeep(origin?.Details.GetValueOrDefault(detail.Key)))
                yield return d;
    }

    internal static FormMetadata IndexForm(this TableMetadata table) =>
        table.Forms.First(x => x.Key == Constants.FormNames.Index).Value;
    internal static FormMetadata BrowseForm(this TableMetadata table) =>
        table.Forms.First(x => x.Key == Constants.FormNames.Browse).Value;
    internal static FormMetadata EditForm(this TableMetadata table) =>
        table.Forms.First(x => x.Key == Constants.FormNames.Edit).Value;

    public static IEnumerable<TableColumn> TableFilters(this TableMetadata table)
    {
        if (table.HasPeriod)
            yield return new TableColumn("Date", ColumnType.Date);
        foreach (var c in table.AllColumns(c => c.IsRef))
            yield return c;
    }

    internal static String? DocumentOperation(this TableMetadata table) =>
        table.IsDocument && table.Columns.Any(c => c.IsOperation) ? table.Path.Split('/')[^1] : null;
}

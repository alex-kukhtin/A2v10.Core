// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.Infrastructure;
using A2v10.Services;

namespace A2v10.Metadata;

internal static class MetadataExtensions
{
    internal static EndpointKind ToEndpointKind(this String schema)
    {
        return schema switch
        {
            Constants.SchemaNames.Catalog => EndpointKind.Catalog,
            Constants.SchemaNames.Document => EndpointKind.Document,
            Constants.SchemaNames.Journal => EndpointKind.Journal,
            "operation" => EndpointKind.Operation,
            _ => throw new InvalidOperationException($"Invalid schema for EndpointKind {schema}")
        };
    }

    internal static String ToSqlSchema(this String folder)
    {
        return folder switch
        {
            Constants.SchemaNames.Catalog => "cat",
            Constants.SchemaNames.Document => "doc",
            "operation" => "op",
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
        var kind = action == "index" || action == "edit" && table.EditWith == EditWithMode.Page ? "_page" : "_dialog";
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
            Table = col.Reference.RefTable,
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
        columns.Where(c => c.IsRef).Select((c, ix) => new RefDescriptor(ix + 1, c, c.RefTable
            ?? throw new InvalidOperationException($"RefTable for {c.Name} is null")));

    internal static FormMetadata IndexForm(this TableMetadata table) =>
        table.Forms.First(x => x.Key == Constants.FormNames.Index).Value;

    internal static FormMetadata EditForm(this TableMetadata table) =>
        table.Forms.First(x => x.Key == Constants.FormNames.Edit).Value;

}

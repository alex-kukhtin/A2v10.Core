// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using A2v10.Infrastructure;
using A2v10.Services;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal static class MetadataExtensions
{
    internal static String ToFolder(this String schema)
    {
        return schema switch
        {
            "cat" => "catalog",
            "doc" => "document",
            "jrn" => "journal",
            "op" => "operation",
            "rep" => "report",
            "acc" => "account",
            "regi" => "inforegister",
            _ => schema
        };
    }
    internal static String FromFolder(this String folder)
    {
        return folder switch
        {
            "catalog" => "cat",
            "document" => "doc",
            "operation" => "op",
            "journal" => "jrn",
            "report" => "rep",
            "account" => "acc",
            "inforegister" => "regi",
            _ => folder
        };
    }
    internal static String EndpointPath(this ColumnReference refs)
    {
        return $"/{refs.RefSchema.ToFolder()}/{refs.RefTable}".ToLowerInvariant();
    }

    internal static Boolean IsEmpty(this ColumnReference? refs)
    {
        if (refs == null) return true;
        if (String.IsNullOrEmpty(refs.RefTable)) return true;
        return false;
    }

    internal static String EndpointPath(this TableMetadata table)
    {
        return $"/{table.Schema.ToFolder()}/{table.Name}".ToLowerInvariant();
    }

    internal static String EndpointPathUseBase(this TableMetadata table, TableMetadata? baseTable)
    {
        if (baseTable != null)
            return baseTable.EndpointPath();
        return table.EndpointPath();
    }

    public static IPlatformUrl PlatformUrl(this TableMetadata table, String action)
    {
        var kind = "_dialog";
        if (action == "index")
            kind = "_page";
        else if (action == "edit" && table.EditWith == EditWithMode.Page)
            kind = "_page";
        var url = $"{kind}/{table.EndpointPath()}/{action}/";
        return new PlatformUrl(url);
    }

    internal static String EditEndpoint(this TableMetadata table, TableMetadata? baseTable)
    {
        var editEndpoint = $"{table.EndpointPathUseBase(baseTable)}/edit";

        if (table.Columns.Any(c => c.DataType == ColumnDataType.Operation))
            editEndpoint = "{Operation.Url}";

        return editEndpoint;
    }

    internal static Boolean HasPeriod(this TableMetadata table)
    {
        return table.IsDocument || table.IsJournal;
    }

    internal static String LocalPath(this TableMetadata table, String action)
    {
        action = action.ToLowerInvariant();
        var path = $"{table.Schema.ToFolder()}/{table.Name}".ToLowerInvariant();

        String CreateEditPath()
        {
            var prefix = table.EditWith == EditWithMode.Page ? "/_page" : "/_dialog";
            return $"{prefix}/{path}/{action}/new";
        }

        return action.ToLowerInvariant() switch
        {
            "index" => $"/_page/{path}/{action}/new",
            "browse" or "browsefolder" or "editfolder" => $"/_dialog/{path}/{action}/new",
            "edit" => CreateEditPath(),
            _ => throw new NotSupportedException($"Invalid Action ({action})")
        };
    }


    internal static IEnumerable<ReportItemMetadata> TypedReportItems(this TableMetadata table, ReportItemKind kind)
    {
        return table.ReportItems.Where(ri => ri.Kind == kind).OrderBy(r => r.Order);
    }

    internal static String Endpoint(this ReportItemMetadata item)
    {
        return $"/{item.RefSchema.ToFolder()}/{item.RefTable}";
    }
    internal static String CreateField(this ReportItemMetadata item, ColumnDataType idDataType, String? prefix = null)
    {
        return $"[{prefix}{item.Column}] {item.DataType.ToSqlDbType(idDataType).ToString().ToLowerInvariant()}";
    }

    internal static TableMetadata CreateOperationMeta()
    {
        return new TableMetadata()
        {
            Schema = "op",
            Name = "Operations",
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
                },
                new TableColumn()
                {
                    Name = "Url",
                    DataType = ColumnDataType.String,
                    MaxLength = 255
                }
            ]
        };
    }
    internal static TableMetadata CreateEnumMeta(TableColumn col)
    {
        return new TableMetadata()
        {
            Schema = col.Reference.RefSchema,
            Name = col.Reference.RefTable,
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
        };
    }

    internal static String LocalizeLabel(this ReportItemMetadata item)
    {
        return item.Label.Localize() ?? $"@[{item.Column}]";
    }

    internal static Bind BindColumn(this ReportItemMetadata item, String? prefix = null)
    {
        return item.DataType switch
        {
            ColumnDataType.Money => new BindSum($"{prefix}{item.Column}"),
            ColumnDataType.Float => new BindNumber($"{prefix}{item.Column}"),
            _ => new Bind($"{prefix}{item.Column}")
        };
    }

    internal static SheetCell BindSheetCell(this ReportItemMetadata item, String? prefix = null)
    {
        var bind = item.DataType switch
        {
            ColumnDataType.Money => new BindSum($"{prefix}{item.Column}"),
            ColumnDataType.Float => new BindNumber($"{prefix}{item.Column}"),
            _ => new Bind($"{prefix}{item.Column}")
        };
        var align = item.DataType switch
        {
            ColumnDataType.Money => TextAlign.Right,
            ColumnDataType.Float => TextAlign.Right,
            _ => TextAlign.Left
        };
        return new SheetCell()
        {
            Align = align,
            Bindings = b => b.SetBinding(nameof(SheetCell.Content), bind)
        };
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

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

}

// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

namespace A2v10.Metadata;

internal enum FormColumnType
{
    String,
    Id,
    Ref,
    Date,
    DateTime,
    Number,
    Currency
}

public enum FormCommandType
{
    Add,
    Edit,
    Delete,
    Open,
    Save,
    SaveAndClose,
    Print,
    Copy,
    Show,
    Search,
    Reload,
    Apply,
    Attachments,
    // dividers
    Sep,
    ToRight
}

public record FormColumn
{
    public String Header { get; set; } = default!;

    public String Path { get; set; } = default!;

    [JsonIgnore]
    internal FormColumnType DataType { get; private set; }

    public void SetDefaults(TableColumn column)
    {
        Header ??= $"@[{column.Name}]";
        Path ??= column.Type == ColumnType.Ref ? $"{column.Name}.{column.Presentation}" : column.Name;
        // set always
        DataType = column.Type.ToFormDataType();
    }
}
public record FormMetadata
{
    public Dictionary<String, FormColumn> Columns { get; set; } = [];
    public FormCommandType[] Commands { get; set; } = [];

    public void SetDefaults(TableMetadata table)
    {
        foreach (var column in Columns)
        {
            var c = table.AllColumns(с => true).FirstOrDefault(c => c.Name == column.Key) 
                ?? throw new InvalidOperationException($"FormMetadata. Column {column.Key} not found");
            column.Value.SetDefaults(c);
        }
    }
}

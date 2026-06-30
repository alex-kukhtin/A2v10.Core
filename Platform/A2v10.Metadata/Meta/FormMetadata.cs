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

public enum EntityCommandType
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
    Post,
    UnPost,
    Attachments
}

public enum CommandBarItemKind 
{ 
    Command, 
    Separator, 
    Aligner 
}

[JsonConverter(typeof(CommandBarItemConverter))]
public readonly struct CommandBarItem
{
    public CommandBarItemKind Kind { get; }
    public EntityCommandType? Command { get; }
    private CommandBarItem(CommandBarItemKind kind, EntityCommandType? command)
    {
        Kind = kind;
        Command = command;
    }

    public static implicit operator CommandBarItem(EntityCommandType command)
        => new(CommandBarItemKind.Command, command);

    public static readonly CommandBarItem Separator = new(CommandBarItemKind.Separator, null);
    public static readonly CommandBarItem Aligner = new(CommandBarItemKind.Aligner, null);
}

public enum FormFilterType
{
    Ref,
    Period
}

public record FormFilter(String Column, FormFilterType? Type);

public record FormColumn
{
    public String Header { get; set; } = default!;
    public String Path { get; set; } = default!;
    [JsonIgnore]
    internal FormColumnType DataType { get; private set; }
    [JsonIgnore]
    internal TableColumn TableColumn { get; private set; } = default!;

    public void SetTableColumn(TableColumn column)
    {
        TableColumn = column;
        Header ??= $"@[{TableColumn.Name}]";
        Path ??= TableColumn.IsRef ? $"{TableColumn.Name}.{TableColumn.Presentation}" : TableColumn.Name;
        // set always
        DataType = TableColumn.Type.ToFormDataType();
    }
}

public abstract record FormElement;

public sealed record FormDataGrid : FormElement
{
    public Dictionary<String, FormColumn> Columns { get; set; } = [];
}

public sealed record FormControl : FormElement
{
    public String Key { get; init; } = default!;
    public FormColumn Column { get; init; } = default!;
}

public sealed record FormGrid : FormElement
{
    public Dictionary<String, FormColumn> Columns { get; set; } = [];
}

public sealed record FormToolbar : FormElement
{
    public List<CommandBarItem> Commands { get; set; } = [];
}

public sealed record FormTaskPad : FormElement
{    
    public List<FormFilter> Filters { get; set; } = [];
}
public sealed record FormPager : FormElement;

public abstract record FormMetadata
{    
    public List<FormElement> Elements { get; set; } = [];
    public List<CommandBarItem> Toolbar { get; set; } = [];
    public FormTaskPad? TaskPad { get; init; }
    public void SetDefaults(TableMetadata table, Func<TableColumn, Boolean> filter)
    {
        var cols = table.AllColumns(filter).ToList();
        foreach (var el in Elements)
        {
            if (el is FormDataGrid dg)
                foreach (var column in dg.Columns)
                {
                    var fc = cols.FirstOrDefault(c => c.Name == column.Key)
                        ?? throw new InvalidOperationException($"FormMetadata. Column {column.Key} not found");
                    column.Value.SetTableColumn(fc);
                }
            else if (el is FormGrid fg)
                foreach (var column in fg.Columns)
                {
                    var fc = cols.FirstOrDefault(c => c.Name == column.Key)
                        ?? throw new InvalidOperationException($"FormMetadata. Column {column.Key} not found");
                    column.Value.SetTableColumn(fc);
                }
        }
    }
}
public sealed record FormPage : FormMetadata;
public sealed record FormDialog : FormMetadata;

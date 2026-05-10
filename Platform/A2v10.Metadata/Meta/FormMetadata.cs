using System;

namespace A2v10.Metadata;

public record FormColumn
{
    public String Field { get; set; } = default!;
    public String Header { get; set; } = default!;

    public void SetDefaults(TableColumn column)
    {
        if (Header == null) 
            Header = $"[{Field}]";
    }
}
public record FormMetadata
{
    public FormColumn[] Columns { get; set; } = [];

    public void SetDefaults(TableMetadata table)
    {
        foreach (var column in Columns)
        {
            if (table.Fields.TryGetValue(column.Field, out TableColumn? tableColumn))
                column.SetDefaults(tableColumn);
            else
                throw new InvalidOperationException($"FormMetadata. Column {column.Field} not found");
        }
    }
}

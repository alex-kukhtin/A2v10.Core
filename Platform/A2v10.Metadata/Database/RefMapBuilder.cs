// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal record RefMapItem(TableMetadata SourceTable, Dictionary<String, TableColumn[]> ByTarget);
internal class RefMapBuilder
{
    private readonly List<RefMapItem> _flat;
    private readonly Dictionary<String, List<String>> _tableStruct;
    private readonly Boolean _isPlain;
    public RefMapBuilder(TableMetadata table, Boolean isPlain)
    {
        _isPlain = isPlain;
        _flat = Flatten(table).ToList();
        _tableStruct = BuildTableStructure();
    }
    public Boolean IsEmpty => _flat.Count == 0;

    private IEnumerable<RefMapItem> Flatten(TableMetadata table)
    {
        yield return new RefMapItem(
            table,
            table.Columns
                .Where(c => c.IsRef)
                .GroupBy(c => $"{c.RefTableCheck.SqlTableName}|{c.RefTableCheck.TypeName}")
                .ToDictionary(g => g.Key, g => g.ToArray())
        );

        if (!_isPlain)
            yield break;
        foreach (var detail in table.Details ?? [])
            foreach (var item in Flatten(detail.Value))
                yield return item;
    }

    private Dictionary<String, List<String>> BuildTableStructure()
    {
        return _flat
            .SelectMany(x => x.ByTarget)
            .GroupBy(kvp => kvp.Key)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var first = g.First().Value.Select(c => c.Name).ToList();
                    var maxCount = g.Max(x => x.Value.Length);
                    return first
                        .Concat(Enumerable
                            .Range(first.Count + 1, maxCount - first.Count)
                            .Select(i => $"col_{i}"))
                        .ToList();
                }
            );
    }

    public String GenerateDeclare()
    {
        var cols = _tableStruct
            .SelectMany(kvp => kvp.Value.Select(name => $"[{name}] bigint"));
        return $"""
        -- map table
        declare @map table({String.Join(", ", cols)});
        """;
    }

    public String GenerateInserts()
    {
        var ins = _flat.Select(item =>
        {
            var mappings = item.ByTarget
                            .SelectMany(kvp => kvp.Value
                                .Select((col, i) => (
                                    Target: $"[{_tableStruct[kvp.Key][i]}]",
                                    Source: $"[{col.Name}]"
                                )))
                            .ToList();

            var targetCols = String.Join(", ", mappings.Select(m => m.Target));
            var sourceCols = String.Join(", ", mappings.Select(m => m.Source));
            var where = item.SourceTable.Kind == EndpointKind.Details
               ? "[Owner] = @Id"
               : "Id = @Id";
            return $"""
            insert into @map({targetCols}) 
            select {sourceCols} 
            from {item.SourceTable.SqlTableName}
            where {where};
            """;
        });

        return String.Join("\n\n", ins);
    }

    public String GenerateResolves()
    {
        var blocks = _tableStruct.Select(kvp =>
        {
            var spl = kvp.Key.Split('|');
            var tableName = spl[0];
            var typeName = spl[1];

            var unionLines = kvp.Value
                .Select(col => $"""
                select id = [{col}] from @map where [{col}] is not null group by [{col}]
                """)                
                .ToList();

            String cte;
            if (unionLines.Count == 1)
                cte = $"""
                -- {typeName} Map
                with T as ({unionLines[0]})
                """;
            else
                cte = $"""
                -- {typeName} Map
                with T as (
                  select id from (
                    {String.Join("\n    union all\n    ", unionLines)}
                  ) ids group by id
                )
                """;
            var select = $"""
            select [!{typeName}!Map] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name 
            from {tableName} a inner join T on a.Id = T.id;
            """;
            return $"{cte}\n{select}";
        });

        return String.Join("\n\n", blocks);
    }
}

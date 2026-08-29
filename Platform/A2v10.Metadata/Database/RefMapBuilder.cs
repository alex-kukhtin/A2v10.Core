// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace A2v10.Metadata;

/* One source of references, and how to reach ITS rows. 'Where' travels with the table because the
 * two are one answer: a header is found by Id, a collection by Owner, a journal by the document
 * that posted it - and nothing downstream can derive which from the table alone.
 */
internal record RefMapItem(TableMetadata SourceTable, String Where,
       Dictionary<String, TableColumn[]> ByTarget
    );
internal class RefMapBuilder
{
    private readonly List<RefMapItem> _flat;
    private readonly Dictionary<String, List<TableColumn>> _tableStruct;
    private readonly Dictionary<String, List<TableColumn>> _inheritStruct;
    private readonly Boolean _isPlain;
    private readonly Boolean _hasDefaults;

    /* The endpoint's own, and null when this map spans SEVERAL tables and therefore belongs to no
     * single endpoint. The three readers that need them - inherits, initials, the document
     * operation - all run on the plain path only, which is never that mode.
     */
    private readonly DeclarationMetadata? _declaration;
    private readonly NormalEndpointMetadata? _endpoint;

    public RefMapBuilder(NormalEndpointMetadata endpoint, Boolean isPlain, Boolean hasDefaults)
    {
        var table = endpoint.Storage;
        _declaration = endpoint.Declaration;
        _endpoint = endpoint;
        _isPlain = isPlain;
        _hasDefaults = hasDefaults;
        _flat = [.. Flatten(table, "Id = @Id")];
        _tableStruct = BuildTableStructure();
        _inheritStruct = BuildInheritStructure();
    }

    /* The UNION of several tables into one map - which is the same machine, fed a list instead of
     * a table and its details: '_flat' has always been a list and '_tableStruct' has always been
     * the union over it. One '@map', one resolve recordset per target, so a catalog three journals
     * point at is fetched once. See CLAUDE.md, "System endpoints" for why this is not an endpoint.
     */
    public RefMapBuilder(IEnumerable<(TableMetadata Table, String Where, IEnumerable<TableColumn> Columns)> sources)
    {
        _isPlain = false;
        _hasDefaults = false;
        _flat = [.. sources.Select(s => RefsOf(s.Table, s.Where, s.Columns))];
        _tableStruct = BuildTableStructure();
        _inheritStruct = [];
    }

    public Boolean IsEmpty => _flat.Count == 0;


    private String RealTypeName(TableMetadata t)
        => _isPlain ? t.TypeName : t.RefTypeName;

    private String TargetKey(TableColumn c)
        => $"{c.RefTableCheck.Storage.SqlTableName}|{RealTypeName(c.RefTableCheck.Storage)}";

    /* The columns are a parameter, not 'table.Columns': the map exists to resolve what a recordset
     * SENT, and a caller that emits a subset would otherwise fetch objects for columns nobody
     * shows - a journal's Document among them, resolved on every row of the very document you are
     * standing on.
     */
    private RefMapItem RefsOf(TableMetadata table, String where, IEnumerable<TableColumn> columns) =>
        new(table, where,
            columns
                .Where(c => c.IsRef)
                .GroupBy(TargetKey)
                .ToDictionary(g => g.Key, g => g.ToArray())
        );

    private IEnumerable<RefMapItem> Flatten(TableMetadata table, String where)
    {
        yield return RefsOf(table, where, table.Columns);

        if (!_isPlain)
            yield break;
        foreach (var detail in table.Details ?? [])
            foreach (var item in Flatten(detail.Value, $"[{Constants.FieldNames.Owner}] = @Id"))
                yield return item;
    }

    private Dictionary<String, List<TableColumn>> BuildInheritStructure()
    {
        if (!_isPlain || _declaration == null)
            return [];

        /* The other half of InheritDescriptor's asymmetry: 'field' and 'ref' belong to the table
         * the rule is written on and are resolved at load, this one belongs to the table the
         * reference points at and can only be asked once the graph is linked. Local because that
         * moment is this method and nothing else - and here it is visible that the third name is
         * the one a typo carries past load, into SQL generation. It says where it looked in the
         * same words the other two do (DeclarationBake.BuildInherits).
         */
        static TableColumn SourceColumn(InheritDescriptor descriptor)
        {
            var refTable = descriptor.Ref.RefTableCheck.Storage;
            return refTable.Columns.FirstOrDefault(c => c.Name == descriptor.Source)
                ?? throw new InvalidOperationException(
                    $"inherit: source '{descriptor.Source}' not found in {refTable.SqlTableName}");
        }

        return _declaration.AllInherits()
            .GroupBy(d => TargetKey(d.Ref))
            .ToDictionary(
                g => g.Key,
                g => g.Select(SourceColumn)
                      .Where(c => c.Type != ColumnType.Id && c.Type != ColumnType.Name)
                      .DistinctBy(c => c.Name)
                      .ToList()
            );
    }
    /* The slots of '@map' for one target: as many as the WIDEST source needs, named after that
     * source's own columns. Positional - GenerateInserts writes a source's i-th column into the
     * i-th slot - so a narrower source simply leaves the tail null.
     *
     * Widest and not first: taking the first and then reaching past its end for the missing names
     * was an index out of range. Unreachable while a header always out-numbered its details;
     * reachable the moment several tables are unioned, where nothing orders them at all.
     */
    private Dictionary<String, List<TableColumn>> BuildTableStructure()
    {
        return _flat
            .SelectMany(x => x.ByTarget)
            .GroupBy(kvp => kvp.Key)
            .ToDictionary(
                g => g.Key,
                g => g.MaxBy(x => x.Value.Length).Value.ToList()
            );
    }

    public String? GenerateDeclare()
    {
        var cols = _tableStruct
            .SelectMany(kvp => kvp.Value.Select(col => $"[{col.Name}] {col.SqlDataType()}"));
        if (!cols.Any())
            return null;
        return $"""
        -- map table
        declare @map table({String.Join(", ", cols)});
        """;
    }

    public String? GenerateInserts()
    {
        var ins = _flat.Select(item =>
        {
            var mappings = item.ByTarget
                            .SelectMany(kvp => kvp.Value
                                .Select((col, i) => (
                                    Target: $"[{_tableStruct[kvp.Key][i].Name}]",
                                    Source: $"[{col.Name}]"
                                )))
                            .ToList();

            var targetCols = String.Join(", ", mappings.Select(m => m.Target));
            var sourceCols = String.Join(", ", mappings.Select(m => m.Source));
            if (String.IsNullOrEmpty(targetCols) || String.IsNullOrEmpty(sourceCols))
                return null;
            return $"""
            insert into @map({targetCols})
            select {sourceCols}
            from {item.SourceTable.SqlTableName}
            where {item.Where};
            """;
        });

        return String.Join("\n\n", ins);
    }

    public String? GenerateResolves()
    {
        var blocks = _tableStruct.Select(kvp =>
        {
            var spl = kvp.Key.Split('|');
            var tableName = spl[0];
            var typeName = spl[1];

            var unionLines = kvp.Value
                .Select(col => $"""
                select id = [{col.Name}] from @map where [{col.Name}] is not null group by [{col.Name}]
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
            var inherits = _inheritStruct.TryGetValue(kvp.Key, out var inh) && inh.Count > 0
                ? ", " + String.Join(", ", inh.Select(c => c.SqlModelColumnName("a", RealTypeName)))
                : String.Empty;

            var select = $"""
            select [!{typeName}!Map] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name{inherits}
            from {tableName} a inner join T on a.Id = T.id;
            """;
            return $"{cte}\n{select}";
        });

        if (!blocks.Any())
            return null;
        return String.Join("\n\n", blocks);
    }

    String? GenerateInitials()
    {
        if (!_hasDefaults || _declaration == null)
            return null;
        if (_declaration.InitialValues.Count == 0)
            return null;
        // from user profile
        var profUser = _declaration.InitialValues.Where(x => x.Value.Source == InitialSource.Profile).ToList();
        if (profUser.Count == 0) 
            return null;
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendJoin(Environment.NewLine, profUser.Select(p => $"declare @Init{p.Key} platformid;"));
        sb.AppendLine();
        sb.AppendLine();
        sb.Append("exec usr.GetProfilePreferences @UserId = @UserId, ");
        sb.AppendJoin(',', profUser.Select(p => $"@{p.Value.Value} = @Init{p.Key} output"));
        sb.AppendLine(";");
        var mapKeys = String.Join(',', profUser.Select(p => p.Key));
        var mapValues = String.Join(",", profUser.Select(p => $"@Init{p.Key}"));
        sb.AppendLine();
        sb.AppendLine($"insert into @map({mapKeys}) values ({mapValues});");
        return sb.ToString();
    }
    
    String? GenerateDocOperations()
    {
        if (!_hasDefaults || _endpoint == null)
            return null;
        var docOps = _endpoint.DocumentOperation();
        if (docOps == null)
            return null;
        return $"insert into @map(Operation) values (N'{docOps}');";
    }
    public void WriteRefMap(StringBuilder sb, Action<StringBuilder>? onInsert = null)
    {
        if (IsEmpty)
            return;
        var declare = GenerateDeclare();
        if (declare != null)
        {
            sb.AppendLine();
            sb.AppendLine(declare);
        }
        var inserts = GenerateInserts();
        if (inserts != null)
        {
            sb.AppendLine();
            sb.AppendLine(inserts);
        }

        var docOps = GenerateDocOperations();
        if (docOps != null)
        {
            sb.AppendLine();
            sb.AppendLine(docOps);
        }

        var initials = GenerateInitials();
        if (initials != null)
        {
            sb.AppendLine();
            sb.AppendLine(initials);
        }

        var resolvers = GenerateResolves();
        if (resolvers != null)
        {
            sb.AppendLine();
            sb.AppendLine(resolvers);
        }
    }

    public void WriteRefMapIndex(StringBuilder sb, Action<StringBuilder>? onInsert = null)
    {
        if (IsEmpty)
            return;

        onInsert?.Invoke(sb);

        var resolvers = GenerateResolves();
        if (resolvers != null)
        {
            sb.AppendLine();
            sb.AppendLine(resolvers);
        }
    }
}

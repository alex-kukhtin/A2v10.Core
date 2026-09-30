// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Metadata;

/* Birth on basis: /document/waybillout/edit/new?BasedOn=123&Base=/document/order. The record is filled
 * into table variables of THIS document's shape, and the load reads them as it reads the tables
 * (LoadRows) - so the recordsets, the map and the inherited values come out exactly as for a stored
 * record, and nothing past the fill knows the record is new. See a2v10-md-skill, concepts/basedon.
 */
internal partial class SqlBuilder
{
    // the query key, for a caller that must know before building which text it builds
    internal String? BasedOnQueryValue() => IsNewModel() ? QueryValue(Constants.FieldNames.BasedOnQuery) : null;

    /* The source is named by the url and not read out of the row: ids are per table. What may be born
     * and how is the SOURCE's file ('basedOn'); where the basis lands is this document's own header.
     */
    internal async Task<String> BuildBirthSqlTextAsync()
    {
        var head = $"{Endpoint.Path}: '?{Constants.FieldNames.BasedOnQuery}='";
        var basePath = QueryValue(Constants.FieldNames.BaseQuery)
            ?? throw new InvalidOperationException(
                $"{head} without '?{Constants.FieldNames.BaseQuery}=' - the address of the document it is based on");
        var (schema, name) = DatabaseMetadataProvider.ParsePath(basePath);
        var source = await _metadataProvider.GetNormalEndpointAsync(DataSource, schema, name);
        var self = DatabaseMetadataProvider.ParsePath(Endpoint.Path);
        var entry = source.Declaration.BasedOn.FirstOrDefault(b => DatabaseMetadataProvider.ParsePath(b.Target) == self)
            ?? throw new InvalidOperationException($"{head}: {source.Path} does not offer {Endpoint.Path} in 'basedOn'");

        var variables = new Dictionary<TableMetadata, String>(ReferenceEqualityComparer.Instance);
        void variableOf(TableMetadata table, String variable)
        {
            variables[table] = variable;
            foreach (var (key, details) in table.Details)
                variableOf(details, $"{variable}${key}");
        }
        variableOf(Table, "@$Record");

        var rows = new LoadRows(t => variables[t], $"cast(N'{PlatformId.Empty}' as {PlatformId.SqlTypeName})");
        return BuildLoadPlainSqlText(rows, BirthPrelude(head, source, entry, variables, rows.Key));
    }

    /* Exactly one field of this header holds the basis: of type basedOn, targeting the source. None, and
     * the birth would forget where it came from; two, and which one is meant would be a guess.
     */
    TableColumn BasisField(String head, NormalEndpointMetadata source)
    {
        var path = DatabaseMetadataProvider.ParsePath(source.Path);
        var fields = Table.Columns.Where(c => c.Type == ColumnType.BasedOn
            && c.Target != null && DatabaseMetadataProvider.ParsePath(c.Target) == path).ToList();
        return fields.Count switch
        {
            1 => fields[0],
            0 => throw new InvalidOperationException(
                $"{head}: {Table.Path} has no field of type 'basedOn' targeting {source.Path} - nowhere to write the basis"),
            _ => throw new InvalidOperationException(
                $"{head}: {String.Join(", ", fields.Select(f => $"[{f.Name}]"))} of {Table.Path} all target {source.Path} - which one holds the basis would be a guess")
        };
    }

    /* The source fills, inherit fills what it left empty, and a column with an initial value is left
     * null where the source gave nothing: the defaults recordset fills it on the client, as for any new
     * record - a record with an empty key takes defaults into its empty fields. Every table of the
     * document gets its variable, a collection not copied stays empty: the load reads them all.
     */
    String BirthPrelude(String head, NormalEndpointMetadata source, BasedOnMetadata entry,
        IReadOnlyDictionary<TableMetadata, String> variables, String key)
    {
        var basis = BasisField(head, source);
        var sourceTable = source.Storage;
        var initials = Endpoint.AllInitials();
        var sb = new StringBuilder($"-- birth on basis of {source.Path}");
        sb.AppendLine();

        foreach (var (table, variable) in variables)
            sb.AppendLine($"declare {variable} table({String.Join(", ",
                table.AllColumns().Select(c => $"[{c.Name}] {c.SqlDataType(toTableType: true)}"))});");

        String headValue(TableColumn column)
        {
            if (column.Type == ColumnType.Id)
                return key;
            if (ReferenceEquals(column, basis))
                return $"s.[{Constants.FieldNames.Id}]";
            return BasedOnMapping.SourceOf(column, Table, sourceTable, entry.Document) is { } from
                ? $"s.[{from.Name}]"
                : initials.ContainsKey(column.Name) ? "null" : column.EmptyLiteral();
        }
        var headColumns = Table.AllColumns().ToList();
        sb.AppendLine($"""
            insert into {variables[Table]} ({String.Join(", ", headColumns.Select(c => $"[{c.Name}]"))})
            select {String.Join(", ", headColumns.Select(headValue))}
            from {sourceTable.SqlTableName} s where s.[{Constants.FieldNames.Id}] = @BasedOn;
            if not exists(select 1 from {variables[Table]})
                throw 60000, N'The document to base on is not found', 0;
            """);
        WriteInherits(sb, variables[Table], Endpoint.Declaration.Inherits, null, null);

        if (BasedOnMapping.Rows(head, entry, Table, sourceTable) is not { } copy)
            return sb.ToString();

        var rowColumns = copy.Target.AllColumns().ToList();
        var sourceRowNo = copy.Source.AllColumns().First(c => c.Type == ColumnType.RowNumber);
        var collection = Endpoint.Declaration.Details[copy.Target.DetailsKey];
        foreach (var (kind, sourceKind) in copy.Kinds)
        {
            // the row's place in the record, not data: its key, its header, its position, its kind
            String rowValue(TableColumn column) => column.Type switch
            {
                ColumnType.Id => "null",
                ColumnType.Master => key,
                ColumnType.RowNumber => $"r.[{sourceRowNo.Name}]",
                ColumnType.RowKind => $"N'{kind}'",
                _ => BasedOnMapping.SourceOf(column, copy.Target, copy.Source, new Dictionary<String, String>()) is { } from
                    ? $"r.[{from.Name}]"
                    : column.EmptyLiteral()
            };
            var filter = sourceKind == null ? String.Empty : $" and r.[{copy.Source.RowKindField}] = N'{sourceKind}'";
            sb.AppendLine($"""
                insert into {variables[copy.Target]} ({String.Join(", ", rowColumns.Select(c => $"[{c.Name}]"))})
                select {String.Join(", ", rowColumns.Select(rowValue))}
                from {copy.Source.SqlTableName} r where r.[{copy.Source.MasterField}] = @BasedOn{filter};
                """);
            var rowSet = collection.RowSets.First(rs => rs.Kind == kind);
            WriteInherits(sb, variables[copy.Target], rowSet.Inherits, kind == null ? null : copy.Target.RowKindField, kind);
        }
        return sb.ToString();
    }

    // what the source left empty, from the record the reference points at - the rule 'inherit' is on input
    static void WriteInherits(StringBuilder sb, String variable, Dictionary<String, InheritDescriptor[]> inherits,
        String? kindField, String? kind)
    {
        var onKind = kindField == null ? String.Empty : $" and v.[{kindField}] = N'{kind}'";
        foreach (var inherit in inherits.Values.SelectMany(i => i))
            sb.AppendLine($"""
                update v set v.[{inherit.Field.Name}] = t.[{inherit.Source}]
                from {variable} v inner join {inherit.Ref.RefTableCheck.Storage.SqlTableName} t on t.[{Constants.FieldNames.Id}] = v.[{inherit.Ref.Name}]
                where v.[{inherit.Field.Name}] is null{onKind};
                """);
    }
}

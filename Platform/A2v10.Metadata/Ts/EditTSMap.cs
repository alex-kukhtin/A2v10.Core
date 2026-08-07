// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class ScriptBuilder
{
    internal Task<String> CreateEditMapTS()
    {
        var refDecl = String.Empty;
        var detailsDecl = String.Empty;

        var refs = Table.AllColumns().AllRefs().ToList();

        var refElems = refs.Select(x => $$"""
        export interface {{x.Table.TypeName}} extends IElement {
        {{String.Join("\n", TsProperties(x.Table))}}
        }

        """);

        IEnumerable<String> detailsFields()
        {
            foreach (var t in Table.Details.Select(x => x.Value))
                foreach (var (_, collection, type) in t.RowSets())
                    yield return $"    readonly {collection}: {type}Array;";
        }

        /* Computed lands on the ELEMENT, always - it is a member of the record, and a kind that
         * computes differently from its neighbour declares different members, which is the whole
         * reason the types are separate. A key that is also a column is already declared there,
         * with its real type; repeating it would be a duplicate member, not a second fact.
         */
        IEnumerable<String> computedMembers(TableMetadata table, RuleMetadata rules)
        {
            var columns = table.Columns
                .Where(c => !c.IsVoid && c.Type != ColumnType.RowVersion)
                .Select(c => c.Name)
                .ToHashSet();
            foreach (var key in rules.Computed.Keys.Where(k => !columns.Contains(k)))
                yield return $"\treadonly {key}: any;";
        }

        // a total lands on the ARRAY - it is what the table footer shows, and the footer belongs
        // to the collection, not to any row in it
        static IEnumerable<String> totalMembers(RuleMetadata rules)
        {
            foreach (var name in rules.Total)
                yield return $"\treadonly {name}: any;";
        }

        if (refElems.Any())
            refDecl = $"\n{String.Join("\n", refElems)}\n";

        /* The one artifact that is genuinely a join: a row's TYPE is its columns, which only the
         * shape knows, plus what was declared for that row set, which only the declaration knows.
         * One lookup, by the collection key, and it cannot miss - the baked declaration has a
         * node for every collection the shape has.
         */
        var detailElems = Table.Details
            .SelectMany(x => Endpoint.Declaration.Details[x.Key].RowSets.Select(rs => $$"""
        export interface {{rs.Type}} extends IArrayElement {
        {{String.Join("\n", TsProperties(x.Value).Concat(computedMembers(x.Value, rs.Rules)))}}
        }

        export interface {{rs.Type}}Array extends IElementArray<{{rs.Type}}> {
        {{String.Join("\n", totalMembers(rs.Rules))}}
        }

        """));

        IEnumerable<String> elemProperties()
        {
            foreach (var p in TsProperties(Table))
                yield return p;
            foreach (var p in computedMembers(Table, Endpoint.Declaration.Rules))
                yield return p;
            var detFields = detailsFields().ToList();
            if (detFields.Count == 0)
                yield break;
            yield return "\t// Details";
            foreach (var df in detFields)
                yield return df;
        }

        if (detailElems.Any())
            detailsDecl = $"\n{String.Join("\n", detailElems)}\n";

        var templ = $$"""

        {{refDecl}}{{detailsDecl}}
        export interface {{Table.TypeName}} extends IElement {
        {{String.Join("\n", elemProperties())}}
        }   

        export interface TRoot extends IRoot {
            readonly {{Table.Model}}: {{Table.TypeName}}; 
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

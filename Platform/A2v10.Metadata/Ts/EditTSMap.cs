// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class TypescriptBuilder
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
            {
                if (t.Kinds.Count == 0)
                    yield return $"    readonly {t.CollectionName}: {t.TypeName}Array;";
                else
                    foreach (var k in t.Kinds)
                        yield return $"    readonly {k}: {t.TypeName}Array;";
            }
        }

        IEnumerable<String> detailsComputed()
        {
            foreach (var t in Table.Details.Select(x => x.Value))
                foreach (var c in t.Columns.Where(c => !String.IsNullOrEmpty(c.Computed)))
                    yield return $"    readonly {c.Name}: any;";
        }

        if (refElems.Any())
            refDecl = $"\n{String.Join("\n", refElems)}\n";

        var detailElems = Table.Details.Select(x => x.Value).Select(d => $$"""
        export interface {{d.TypeName}} extends IArrayElement {
        {{String.Join("\n", TsProperties(d))}}
        }

        export interface {{d.TypeName}}Array extends IElementArray<{{d.TypeName}}> {
        {{String.Join("\n", detailsComputed())}}
        }

        """);

        IEnumerable<String> elemProperties()
        {
            foreach (var p in TsProperties(Table))
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

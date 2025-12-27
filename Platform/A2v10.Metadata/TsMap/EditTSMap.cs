// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class PlainModelBuilder
{
    internal Task<String> CreateMapTS()
    {
        var refDecl = String.Empty;
        var detailsDecl = String.Empty;    

        var refElems = _refFields.RefTables().Select(x => $$"""
        export interface {{x.RealTypeName}} extends IElement {
        {{String.Join("\n", TsProperties(x))}}
        }

        """);

        IEnumerable<String> detailsFields()
        {
            foreach (var t in _table.Details)
                foreach (var k in t.Kinds)
                    yield return $"    readonly {k.Name}: {t.RealTypeName}Array;";
        }

        IEnumerable<String> detailsComputed()
        {
            foreach (var t in _table.Details)
                foreach (var c in t.Columns.Where(c => !String.IsNullOrEmpty(c.Computed)))
                    yield return $"    readonly {c.Name}: any;";
        }

        if (refElems.Any())
            refDecl = $"\n{String.Join("\n", refElems)}\n";

        var detailElems = _table.Details.Select(d => $$"""
        export interface {{d.RealTypeName}} extends IArrayElement {
        {{String.Join("\n", TsProperties(d))}}
        }

        export interface {{d.RealTypeName}}Array extends IElementArray<{{d.RealTypeName}}> {
        {{String.Join("\n", detailsComputed())}}
        }

        """);

        if (detailElems.Any())
            detailsDecl = $"\n{String.Join("\n", detailElems)}\n";

        var templ = $$"""

        {{refDecl}}{{detailsDecl}}
        export interface {{_table.RealTypeName}} extends IElement {
        {{String.Join("\n", TsProperties(_table))}}
        }   

        export interface TRoot extends IRoot {
            readonly {{_table.RealItemName}}: {{_table.RealTypeName}}; 
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

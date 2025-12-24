// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class PlainModelBuilder
{
    internal Task<String> CreateMapTS()
    {
        var refDecl = String.Empty;

        var refElems = _refFields.Select(x => $$"""
        export interface {{x.Table.RealTypeName}} extends IElement {
        {{String.Join("\n", TsProperties(x.Table))}}
        }
        """);

        if (refElems.Any())
            refDecl = $"\n{String.Join("\n", refElems)}\n";

        var templ = $$"""

        {{refDecl}}
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

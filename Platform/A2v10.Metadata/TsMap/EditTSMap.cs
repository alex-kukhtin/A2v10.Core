// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class PlainModelBuilder
{
    internal Task<String> CreateMapTS()
    {
        var templ = $$"""

        export interface {{_table.RealTypeName}} extends IElement {
        {{String.Join("\n", _table.TsProperties(_appMeta))}}
        }   

        export interface TRoot extends IRoot {
            readonly {{_table.RealItemName}}: {{_table.RealTypeName}}; 
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

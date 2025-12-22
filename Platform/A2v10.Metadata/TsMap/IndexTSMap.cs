// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
{
    internal Task<String> CreateMapTS()
    {
        var collType = $"T{_table.RealItemsName}";

        var templ = $$"""

        export interface {{_table.RealTypeName}} extends IArrayElement {
        {{String.Join("\n", _table.TsProperties(_appMeta))}}
        }

        declare type {{collType}} = IElementArray<{{_table.RealTypeName}}>;

        export interface TRoot extends IRoot {
            readonly {{_table.RealItemsName}}: {{collType}};
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

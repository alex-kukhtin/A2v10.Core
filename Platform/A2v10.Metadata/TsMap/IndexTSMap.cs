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
        String property(TableColumn column)
        {
            var ro = column.IsFieldUpdated() ? "" : "readonly ";
            return $"\t{ro}{column.Name}: {column.DataType.ToTsType(_appMeta.IdDataType)};";
        }

        IEnumerable<String> properties()
        {
            foreach (var p in _table.Columns.Where(c => !c.IsVoid))
                yield return property(p);
        }

        var collType = $"T{_table.RealItemsName}";

        var templ = $$"""
        export interface {{_table.RealTypeName}} extends IArrayElement {
        {{String.Join("\n", properties())}}
        }

        declare type {{collType}} = IElementArray<{{_table.RealTypeName}}>;

        export interface TRoot extends IRoot {
            readonly {{_table.RealItemsName}}: {{collType}};
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

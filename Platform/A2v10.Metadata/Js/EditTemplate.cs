// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    private Task<String> CreateEditTemplate()
    {
        return _table.Schema switch
        {
            "doc" => CreateDocumentTemplate(),
            _ => CreateGenericEditTemplate()
        };
    }

    IEnumerable<String> properties()
    {
        if (_table.Details.Count > 0)
        {
            var fd = _table.Details.First();
            yield return $$"""'{{_table.RealTypeName}}.$$Tab': {type: String, value: '{{fd.RealItemsName}}'}""";
        }
    }

    IEnumerable<String> validators()
    {
        foreach (var col in _table.Columns.Where(c => c.Required))
            yield return $"'{_table.RealItemName}.{col.Name}': `@[Error.Required]`";
        foreach (var d in _table.Details)
            foreach (var c in d.Columns.Where(c => c.Required))
                yield return $"'{_table.RealItemName}.{d.RealItemsName}[].{c.Name}': `@[Error.Required]`";
    }

    private Task<String> CreateGenericEditTemplate()
    {
        const String jsDivider = ",\n\t\t";

        var templ = $$"""
        const template = {
            properties: {
                {{String.Join(jsDivider, properties())}}
            },
            validators: {
                {{String.Join(jsDivider, validators())}}
            },
        };
        module.exports = template;
        """;
        return Task.FromResult<String>(templ);
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class PlainModelBuilder
{
    internal Task<String> CreateEditTemplate()
    {
        return _table.Schema switch
        {
            "doc" => CreateDocumentTemplate(),
            _ => CreateGenericEditTemplate()
        };
    }

    private Task<String> CreateGenericEditTemplate()
    {

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
            foreach (var col in _table.Columns.Where(c => c.Required || c.Unique))
            {
                if (col.Unique && col.Required)
                    yield return $$"""
                '{{_table.RealItemName}}.{{col.Name}}': [
                    `@[Error.Required]`,
                    {valid: {{col.Name.ToLowerInvariant()}}Duplicate, async: true, msg: `@[Error.Duplicate]`}]
                """;
                else if (col.Required)
                    yield return $"'{_table.RealItemName}.{col.Name}': `@[Error.Required]`";
                else if (col.Unique)
                    yield return $$"""'{{_table.RealItemName}}.{{col.Name}}': {valid: {{col.Name.ToLowerInvariant()}}Duplicate, async: true, msg: `@[Error.{{_table.RealItemName}}.Duplicate.{{col.Name}}]`}""";
            }

            foreach (var d in _table.Details)
                foreach (var c in d.Columns.Where(c => c.Required))
                    yield return $"'{_table.RealItemName}.{d.RealItemsName}[].{c.Name}': `@[Error.Required]`";
        }

        IEnumerable<String> functions()
        {
            foreach (var c in _table.Columns.Where(c => c.Unique))
            {
                yield return $$"""
                function {{c.Name.ToLowerInvariant()}}Duplicate(el, val) {
                    if (!val) return true;
                    return el.$vm.$asyncValid('{{c.Name}}.Unique', {Id: el.Id, Value: val});
                }
                """;
            }
        }

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

        {{String.Join('\n', functions())}}
        """;
        return Task.FromResult<String>(templ);
    }
}

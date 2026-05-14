// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class JavascriptBuilder
{
    internal Task<String> CreateEditTemplate()
    {
        return Table.Schema switch
        {
            "doc" => CreateDocumentTemplate(),
            _ => CreateGenericEditTemplate()
        };
    }

    private Task<String> CreateGenericEditTemplate()
    {

        IEnumerable<String> properties()
        {
            if (Table.Details.Count > 0)
            {
                var fd = Table.Details.Select(x => x.Value).First();
                yield return $$"""'{{Table.TypeName}}.$$Tab': {type: String, value: '{{fd.CollectionName}}'}""";
            }
        }

        IEnumerable<String> validators()
        {
            foreach (var col in Table.Columns.Where(c => c.Required || c.Unique))
            {
                if (col.Unique && col.Required)
                    yield return $$"""
                '{{Table.Model}}.{{col.Name}}': [
                    `@[Error.Required]`,
                    {valid: {{col.Name.ToLowerInvariant()}}Duplicate, async: true, msg: `@[Error.Duplicate]`}]
                """;
                else if (col.Required)
                    yield return $"'{Table.Model}.{col.Name}': `@[Error.Required]`";
                else if (col.Unique)
                    yield return $$"""'{{Table.Model}}.{{col.Name}}': {valid: {{col.Name.ToLowerInvariant()}}Duplicate, async: true, msg: `@[Error.{{Table.CollectionName}}.Duplicate.{{col.Name}}]`}""";
            }

            foreach (var d in Table.Details.Select(x => x.Value))
                foreach (var c in d.Columns.Where(c => c.Required))
                    yield return $"'{Table.RealItemName}.{d.CollectionName}[].{c.Name}': `@[Error.Required]`";
        }

        IEnumerable<String> functions()
        {
            foreach (var c in Table.Columns.Where(c => c.Unique))
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

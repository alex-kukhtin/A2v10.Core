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
        return Endpoint.Storage.Kind switch
        {
            EndpointKind.Document => CreateDocumentTemplate(),
            _ => CreateGenericEditTemplate()
        };
    }

    private Task<String> CreateGenericEditTemplate()
    {
        var table = Endpoint.Storage;
        IEnumerable<String> properties()
        {
            if (table.Details.Count > 0)
            {
                var fd = table.Details.First();
                if (fd.Value.Kinds.Count > 0)
                    yield return $$"""'{{table.TypeName}}.$$Tab': {type: String, value: '{{fd.Value.Kinds[0]}}'}""";
                else
                    yield return $$"""'{{table.TypeName}}.$$Tab': {type: String, value: '{{fd.Key}}'}""";
            }
        }

        IEnumerable<String> validators()
        {
            var required = table.RequiredFields(Endpoint.Declaration).ToHashSet();
            foreach (var col in table.AllColumns(c => c.Unique || required.Contains(c.Name)))
            {
                if (col.Unique && required.Contains(col.Name))
                    yield return $$"""
                '{{table.Model}}.{{col.Name}}': [
                    `@[Error.Required]`,
                    {valid: {{col.Name.ToLowerInvariant()}}Duplicate, async: true, msg: `@[Error.Duplicate]`}]
                """;
                else if (required.Contains(col.Name))
                    yield return $"'{table.Model}.{col.Name}': `@[Error.Required]`";
                else if (col.Unique)
                    yield return $$"""'{{table.Model}}.{{col.Name}}': {valid: {{col.Name.ToLowerInvariant()}}Duplicate, async: true, msg: `@[Error.{{table.CollectionName}}.Duplicate.{{col.Name}}]`}""";
            }

            foreach (var (name, d) in table.Details)
            {
                // rows are two-layer as well: columns from the shape, rules from the declaration
                var declared = Endpoint.Declaration.Details.GetValueOrDefault(name);
                if (declared == null)
                    continue;
                foreach (var f in d.RequiredFields(declared))
                    yield return $"'{table.Model}.{d.CollectionName}[].{f}': `@[Error.Required]`";
            }
        }

        IEnumerable<String> functions()
        {
            foreach (var c in table.Columns.Where(c => c.Unique))
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

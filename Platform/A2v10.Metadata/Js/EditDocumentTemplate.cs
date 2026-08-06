// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class JavascriptBuilder
{

    private Task<String> CreateDocumentTemplate()
    {
        var table = Endpoint.Storage;

        IEnumerable<String> defaults()
        {
            if (Endpoint.Kind == EndpointKind.Operation)
            {
                var opColumn = table.Columns.FirstOrDefault(c => c.Type == ColumnType.Operation);
                if (opColumn != null)
                    yield return $$"""'{{table.Model}}.{{opColumn.Name}}'() { return { Id: '{{Endpoint.Name}}', Name: '{{Endpoint.Storage.Model}}'};}""";
            }
        }

        IEnumerable<String> properties()
        {
            if (table.Details.Count > 0)
            {
                var fd = table.Details.Select(x => x.Value).First();
                if (fd.Kinds.Count == 0)
                    yield return $$"""'{{table.TypeName}}.$$Tab': {type: String, value: '{{fd.Table}}'}""";
                else
                    yield return $$"""'{{table.TypeName}}.$$Tab': {type: String, value: '{{fd.Kinds.First()}}'}""";
            }
            foreach (var (key, expr) in Endpoint.Declaration.Rules.Computed)
                yield return $$"""'{{table.TypeName}}.{{key}}'() { return {{expr}};}""";

            foreach (var (name, d) in table.Details)
            {
                // rows are two-layer as well: columns from the shape, rules from the declaration
                var declared = Endpoint.Declaration.Details.GetValueOrDefault(name);
                foreach (var (key, expr) in declared?.Rules.Computed ?? [])
                    yield return $$"""'{{d.TypeName}}.{{key}}'() { return {{expr}};}""";
                foreach (var c in d.Columns.Where(c => c.Total))
                    yield return $$"""'{{d.TypeName}}Array.{{c.Name}}'() { return this.$sum(c => c.{{c.Name}}); }""";
            }
        }

        IEnumerable<String> validators()
        {
            foreach (var f in table.RequiredFields(Endpoint.Declaration))
                yield return $"'{table.Model}.{f}': `@[Error.Required]`";

            foreach (var (name, d) in table.Details)
            {
                var declared = Endpoint.Declaration.Details.GetValueOrDefault(name);
                if (declared == null)
                    continue;
                var required = d.RequiredFields(declared);
                if (d.Kinds.Count > 0)
                    foreach (var k in d.Kinds)
                        foreach (var f in required)
                            yield return $"'{table.Model}.{k}[].{f}': `@[Error.Required]`";
                else
                    foreach (var f in required)
                        yield return $"'{table.Model}.{d.CollectionName}[].{f}': `@[Error.Required]`";
            }
        }

        IEnumerable<String> events()
        {
            foreach (var g in table.AllInherits(Endpoint.Declaration).GroupBy(x => x.Ref.Name))
            {
                var body = String.Join(" ", g.Select(x => $"doc.{x.Field.Name} = doc.{x.Ref.Name}.{x.Source.Name};"));
                yield return $$"""'{{table.Model}}.{{g.Key}}.change'(doc) { {{body}} }""";
            }

            foreach (var d in table.Details)
            {
                var dt = d.Value;
                var declared = Endpoint.Declaration.Details.GetValueOrDefault(d.Key);
                if (declared == null)
                    continue;
                var inherits = dt.AllInherits(declared).ToList();
                if (inherits.Count == 0)
                    continue;
                List<String> paths = dt.Kinds.Count > 0
                    ? [.. dt.Kinds.Select(k => $"{table.Model}.{k}[]")]
                    : [$"{table.Model}.{dt.CollectionName}[]"];
                foreach (var g in inherits.GroupBy(x => x.Ref.Name))
                {
                    var body = String.Join(" ", g.Select(x => $"row.{x.Field.Name} = row.{x.Ref.Name}.{x.Source.Name};"));
                    foreach (var p in paths)
                        yield return $$"""'{{p}}.{{g.Key}}.change'(row) { {{body}} }""";
                }
            }
        }

        const String jsDivider = ",\n\t\t";

        var endpoint = Endpoint.Path;
        var templ = $$"""
        const template = {
            options: {
                globalSaveEvent: 'g.document.saved'
            },
            properties: {
                {{String.Join(jsDivider, properties())}}
            },
            defaults: {
                {{String.Join(jsDivider, defaults())}}
            },
            validators: {
                {{String.Join(jsDivider, validators())}}
            },
            events: {
                {{String.Join(jsDivider, events())}}
            },
            commands: {
                post,
                unPost
            }
        };

        module.exports = template;

        async function post() {
            const ctrl = this.$ctrl;
            await ctrl.$invoke('post', {Id: this.{{table.Model}}.Id}, '{{endpoint}}');
        	this.{{table.Model}}.Done = true;
            ctrl.$emitGlobal('g.document.posted', this);
            ctrl.$requery();
        }

        async function unPost() {
            const ctrl = this.$ctrl;
            await ctrl.$invoke('unpost', {Id: this.{{table.Model}}.Id}, '{{endpoint}}');
        	this.{{table.Model}}.Done = false;
            ctrl.$emitGlobal('g.document.posted', this);
            ctrl.$requery();
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

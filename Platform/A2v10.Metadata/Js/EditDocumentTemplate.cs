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
            if (table.Columns.Any(c => c.Type == ColumnType.Date))
                yield return $$"""'{{table.Model}}.Date'() { return du.today(); }""";
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
            foreach (var c in Endpoint.Declaration.Rules.Where(c => !String.IsNullOrEmpty(c.Value.Value)))
                yield return $$"""'{{table.TypeName}}.{{c.Key}}'() { return {{c.Value.Value}};}""";

            foreach (var (name, d) in table.Details)
            {
                // rows are two-layer as well: columns from the shape, rules from the declaration
                var declared = Endpoint.Declaration.Details.GetValueOrDefault(name);
                foreach (var c in (declared?.Rules ?? []).Where(c => !String.IsNullOrEmpty(c.Value.Value)))
                    yield return $$"""'{{d.TypeName}}.{{c.Key}}'() { return {{c.Value.Value}};}""";
                foreach (var c in d.Columns.Where(c => c.Total))
                    yield return $$"""'{{d.TypeName}}Array.{{c.Name}}'() { return this.$sum(c => c.{{c.Name}}); }""";
            }
        }

        IEnumerable<String> validators()
        {
            foreach (var col in table.Columns.Where(c => c.Required))
                yield return $"'{table.Model}.{col.Name}': `@[Error.Required]`";

            foreach (var d in table.Details.Select(x => x.Value))
            {
                if (d.Kinds.Count > 0)
                    foreach (var k in d.Kinds)
                        foreach (var c in d.Columns.Where(c => c.Required))
                            yield return $"'{table.Model}.{k}[].{c.Name}': `@[Error.Required]`";
                else
                    foreach (var c in d.Columns.Where(c => c.Required))
                        yield return $"'{table.Model}.{d.CollectionName}[].{c.Name}': `@[Error.Required]`";
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
                var inherits = dt.AllInherits(Endpoint.Declaration.Details.GetValueOrDefault(d.Key)).ToList();
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
        const du = require('std:utils').date;
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

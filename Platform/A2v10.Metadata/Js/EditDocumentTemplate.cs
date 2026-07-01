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
        IEnumerable<String> defaults()
        {
            if (Table.Columns.Any(c => c.Type == ColumnType.Date))
                yield return $$"""'{{Table.Model}}.Date'() { return du.today(); }""";
            if (Table.Origin != null && Table.Origin.IsOperation)
            {
                var opColumn = Table.Columns.FirstOrDefault(c => c.Type == ColumnType.Operation);
                if (opColumn != null)
                    yield return $$"""'{{Table.Model}}.{{opColumn.Name}}'() { return { Id: '{{Table.Origin.Table.ToLowerInvariant()}}', Name: '{{Table.Origin.Model}}'};}""";
            }
        }

        IEnumerable<String> properties()
        {
            if (Table.Details.Count > 0)
            {
                var fd = Table.Details.Select(x => x.Value).First();
                if (fd.Kinds.Count == 0)
                    yield return $$"""'{{Table.TypeName}}.$$Tab': {type: String, value: '{{fd.Table}}'}""";
                else
                    yield return $$"""'{{Table.TypeName}}.$$Tab': {type: String, value: '{{fd.Kinds.First()}}'}""";
            }
            foreach (var c in Table.Columns.Where(c => !String.IsNullOrEmpty(c.Computed)))
                yield return $$"""'{{Table.TypeName}}.{{c.Name}}'() { return {{c.Computed}};}""";

            foreach (var d in Table.Details.Select(x => x.Value))
            {
                foreach (var c in d.Columns.Where(c => !String.IsNullOrEmpty(c.Computed)))
                    yield return $$"""'{{d.TypeName}}.{{c.Name}}'() { return {{c.Computed}};}""";
                foreach (var c in d.Columns.Where(c => c.Total))
                    yield return $$"""'{{d.TypeName}}Array.{{c.Name}}'() { return this.$sum(c => c.{{c.Name}}); }""";
            }
        }

        IEnumerable<String> validators()
        {
            foreach (var col in Table.Columns.Where(c => c.Required))
                yield return $"'{Table.Model}.{col.Name}': `@[Error.Required]`";

            foreach (var d in Table.Details.Select(x => x.Value))
            {
                if (d.Kinds.Count > 0)
                    foreach (var k in d.Kinds)
                        foreach (var c in d.Columns.Where(c => c.Required))
                            yield return $"'{Table.Model}.{k}[].{c.Name}': `@[Error.Required]`";
                else
                    foreach (var c in d.Columns.Where(c => c.Required))
                        yield return $"'{Table.Model}.{d.CollectionName}[].{c.Name}': `@[Error.Required]`";
            }
        }


        const String jsDivider = ",\n\t\t";

        var endpoint = Table.EndpointPathUseBase(Table.Origin);
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
            commands: {
                post,
                unPost
            }
        };

        module.exports = template;

        async function post() {
            const ctrl = this.$ctrl;
            await ctrl.$invoke('post', {Id: this.{{Table.Model}}.Id}, '{{endpoint}}');
        	this.{{Table.Model}}.Done = true;
            ctrl.$emitGlobal('g.document.posted', this);
            ctrl.$requery();
        }

        async function unPost() {
            const ctrl = this.$ctrl;
            await ctrl.$invoke('unpost', {Id: this.{{Table.Model}}.Id}, '{{endpoint}}');
        	this.{{Table.Model}}.Done = false;
            ctrl.$emitGlobal('g.document.posted', this);
            ctrl.$requery();
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

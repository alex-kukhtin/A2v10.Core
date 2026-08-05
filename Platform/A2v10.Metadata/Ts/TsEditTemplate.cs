// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class TypescriptBuilder
{
    internal Task<String> CreateEditTSTemplate()
    {
        return Table.Kind switch
        {
            EndpointKind.Document => CreateDocumentTSTemplate(),
            _ => CreateGenericEditTSTemplate()
        };
    }

    private Task<String> CreateGenericEditTSTemplate()
    {

        IEnumerable<String> properties()
        {
            if (Table.Details.Count > 0)
            {
                var fd = Table.Details.First().Value;
                yield return $$"""'{{Table.TypeName}}.$$Tab': {type: String, value: '{{fd.CollectionName}}'}""";
            }
        }

        IEnumerable<String> validators()
        {
            var required = Table.RequiredFields(Endpoint.Declaration).ToHashSet();
            foreach (var col in Table.AllColumns(c => c.Unique || required.Contains(c.Name)))
            {
                if (col.Unique && required.Contains(col.Name))
                    yield return $$"""
                '{{Table.Model}}.{{col.Name}}': [
                    `@[Error.Required]`,
                    {valid: {{col.Name.ToLowerInvariant()}}Duplicate, async: true, msg: `@[Error.Duplicate]`}]
                """;
                else if (required.Contains(col.Name))
                    yield return $"'{Table.Model}.{col.Name}': `@[Error.Required]`";
                else if (col.Unique)
                    yield return $$"""'{{Table.Model}}.{{col.Name}}': {valid: {{col.Name.ToLowerInvariant()}}Duplicate, async: true, msg: `@[Error.{{Table.CollectionName}}.Duplicate.{{col.Name}}]`}""";
            }

            foreach (var (name, d) in Table.Details)
            {
                // rows are two-layer as well: columns from the shape, rules from the declaration
                var declared = Endpoint.Declaration.Details.GetValueOrDefault(name);
                if (declared == null)
                    continue;
                foreach (var f in d.RequiredFields(declared))
                    yield return $"'{Table.Model}.{d.CollectionName}[].{f}': `@[Error.Required]`";
            }
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

        IEnumerable<String> types()
        {
            yield return "TRoot";
            yield return Table.TypeName;
            //foreach (var r in _refFields.RefTables(Table.TypeName))
                //yield return r.TypeName;
        }

        const String jsDivider = ",\n\t\t";

        var propsList = properties().ToList();
        var validatorsList = validators().ToList();

        IEnumerable<String> templateProps()
        {
            if (propsList.Count > 0)
                yield return $$"""
                        properties: {
                            {{String.Join(jsDivider, propsList)}}
                        }
                    """;
            if (validatorsList.Count > 0)
                yield return $$"""
                        validators: {
                            {{String.Join(jsDivider, validatorsList)}}
                        }
                    """;
        }

        var templ = $$"""

        import { {{String.Join(", ", types())}} } from './edit';

        const template : Template = {
        {{String.Join(",", templateProps())}}
        };

        export default template;

        {{String.Join('\n', functions())}}
        """;
        return Task.FromResult<String>(templ);
    }

    private Task<String> CreateDocumentTSTemplate()
    {
        IEnumerable<String> defaults()
        {
            if (Table.Columns.Any(c => c.Name == "Date"))
                yield return $$"""'{{Table.Model}}.Date'() { return du.today(); }""";
            if (Endpoint.Kind == EndpointKind.Operation)
            {
                var opColumn = Table.Columns.FirstOrDefault(c => c.Type == ColumnType.Operation);
                if (opColumn != null)
                    yield return $$"""'{{Table.Model}}.{{opColumn.Name}}'() { return { Id: '{{Endpoint.Name}}', Name: '{{Endpoint.Storage.Model}}'};}""";
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
            foreach (var (key, expr) in Endpoint.Declaration.Rules.Computed)
                yield return $$"""'{{Table.TypeName}}.{{key}}'(this: {{Table.TypeName}}) { return {{expr}};}""";

            foreach (var (name, d) in Table.Details)
            {
                // rows are two-layer as well: columns from the shape, rules from the declaration
                var declared = Endpoint.Declaration.Details.GetValueOrDefault(name);
                foreach (var (key, expr) in declared?.Rules.Computed ?? [])
                    yield return $$"""'{{d.TypeName}}.{{key}}'(this: {{d.TypeName}}) { return {{expr}};}""";
                foreach (var c in d.Columns.Where(c => c.Total))
                    yield return $$"""'{{d.TypeName}}Array.{{c.Name}}'(this: {{d.TypeName}}Array) { return this.$sum(c => c.{{c.Name}}); }""";
            }
        }

        IEnumerable<String> validators()
        {
            foreach (var f in Table.RequiredFields(Endpoint.Declaration))
                yield return $"'{Table.Model}.{f}': `@[Error.Required]`";

            foreach (var (name, d) in Table.Details)
            {
                var declared = Endpoint.Declaration.Details.GetValueOrDefault(name);
                if (declared == null)
                    continue;
                var required = d.RequiredFields(declared);
                if (d.Kinds.Count > 0)
                {
                    foreach (var k in d.Kinds)
                        foreach (var f in required)
                            yield return $"'{Table.Model}.{k}[].{f}': `@[Error.Required]`";
                }
                else
                {
                    foreach (var f in required)
                        yield return $"'{Table.Model}.{d.CollectionName}[].{f}': `@[Error.Required]`";
                }
            }
        }

        IEnumerable<String> events()
        {
            foreach (var g in Table.AllInherits(Endpoint.Declaration).GroupBy(x => x.Ref.Name))
            {
                var body = String.Join(" ", g.Select(x => $"doc.{x.Field.Name} = doc.{x.Ref.Name}.{x.Source.Name};"));
                yield return $$"""'{{Table.Model}}.{{g.Key}}.change'(doc: {{Table.TypeName}}) { {{body}} }""";
            }

            foreach (var d in Table.Details)
            {
                var dt = d.Value;
                var declared = Endpoint.Declaration.Details.GetValueOrDefault(d.Key);
                if (declared == null)
                    continue;
                var inherits = dt.AllInherits(declared).ToList();
                if (inherits.Count == 0)
                    continue;
                List<String> paths = dt.Kinds.Count > 0
                    ? [.. dt.Kinds.Select(k => $"{Table.Model}.{k}[]")]
                    : [$"{Table.Model}.{dt.CollectionName}[]"];
                foreach (var g in inherits.GroupBy(x => x.Ref.Name))
                {
                    var body = String.Join(" ", g.Select(x => $"row.{x.Field.Name} = row.{x.Ref.Name}.{x.Source.Name};"));
                    foreach (var p in paths)
                        yield return $$"""'{{p}}.{{g.Key}}.change'(row: {{dt.TypeName}}) { {{body}} }""";
                }
            }
        }

        IEnumerable<String> types()
        {
            yield return "TRoot";
            yield return Table.TypeName;
            //foreach (var r in _refFields.RefTables())
                //yield return r.TypeName;
            foreach (var d in Table.Details.Select(x => x.Value))
            {
                yield return d.TypeName;
                yield return $"{d.TypeName}Array";
            }
        }

        const String jsDivider = ",\n\t\t";

        var endpoint = Endpoint.Path;
        var templ = $$"""

        import { {{String.Join(", ", types())}} } from './edit';

        const du: UtilsDate = require('std:utils').date;
        const template: Template = {
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
                apply,
                unApply
            }
        };

        export default template;

        async function apply(this: TRoot) {
            const ctrl: IController = this.$ctrl;
            await ctrl.$invoke('apply', {Id: this.{{Table.Model}}.Id}, '{{endpoint}}');
        	this.{{Table.Model}}.Done = true;
            ctrl.$emitGlobal('g.document.applied', this);
            ctrl.$requery();
        }

        async function unApply(this: TRoot) {
            const ctrl: IController = this.$ctrl;
            await ctrl.$invoke('unapply', {Id: this.{{Table.Model}}.Id}, '{{endpoint}}');
        	this.{{Table.Model}}.Done = false;
            ctrl.$emitGlobal('g.document.applied', this);
            ctrl.$requery();
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

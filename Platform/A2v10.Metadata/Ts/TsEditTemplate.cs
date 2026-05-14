// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class TypescriptBuilder
{
    internal Task<String> CreateEditTSTemplate()
    {
        return Table.Schema switch
        {
            "doc" => CreateDocumentTSTemplate(),
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
                    yield return $"'{Table.Model}.{d.CollectionName}[].{c.Name}': `@[Error.Required]`";
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
                yield return $$"""'{{Table.RealItemName}}.Date'() { return du.today(); }""";
            if (Table.Origin != null && Table.Origin.IsOperation)
            {
                var opColumn = Table.Columns.FirstOrDefault(c => c.Type == ColumnType.Operation);
                if (opColumn != null)
                    yield return $$"""'{{Table.RealItemName}}.{{opColumn.Name}}'() { return { Id: '{{Table.Origin.Table.ToLowerInvariant()}}', Name: '{{Table.Origin.Model}}'};}""";
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
                    yield return $$"""'{{Table.TypeName}}.$$Tab': {type: String, value: '{{fd.Kinds.First().Name}}'}""";
            }
            foreach (var c in Table.Columns.Where(c => !String.IsNullOrEmpty(c.Computed)))
                yield return $$"""'{{Table.TypeName}}.{{c.Name}}'(this: {{Table.TypeName}}) { return {{c.Computed}};}""";

            foreach (var d in Table.Details.Select(x => x.Value))
            {
                foreach (var c in d.Columns.Where(c => !String.IsNullOrEmpty(c.Computed)))
                    yield return $$"""'{{d.TypeName}}.{{c.Name}}'(this: {{d.TypeName}}) { return {{c.Computed}};}""";
                foreach (var c in d.Columns.Where(c => c.Total))
                    yield return $$"""'{{d.TypeName}}Array.{{c.Name}}'(this: {{d.TypeName}}Array) { return this.$sum(c => c.{{c.Name}}); }""";
            }
        }

        IEnumerable<String> validators()
        {
            foreach (var col in Table.Columns.Where(c => c.Required))
                yield return $"'{Table.Model}.{col.Name}': `@[Error.Required]`";

            foreach (var d in Table.Details.Select(x => x.Value))
            {
                if (d.Kinds.Count > 0)
                {
                    foreach (var k in d.Kinds)
                        foreach (var c in d.Columns.Where(c => c.Required))
                            yield return $"'{Table.Model}.{k.Name}[].{c.Name}': `@[Error.Required]`";
                }
                else
                {
                    foreach (var c in d.Columns.Where(c => c.Required))
                        yield return $"'{Table.RealItemName}.{d.RealItemsName}[].{c.Name}': `@[Error.Required]`";
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

        var endpoint = Table.EndpointPathUseBase(Table.Origin);
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
            commands: {
                apply,
                unApply
            }
        };

        export default template;

        async function apply(this: TRoot) {
            const ctrl: IController = this.$ctrl;
            await ctrl.$invoke('apply', {Id: this.{{Table.RealItemName}}.{{Table.PrimaryKeyField}}}, '{{endpoint}}');
        	this.{{Table.RealItemName}}.Done = true;
            ctrl.$emitGlobal('g.document.applied', this);
            ctrl.$requery();
        }

        async function unApply(this: TRoot) {
            const ctrl: IController = this.$ctrl;
            await ctrl.$invoke('unapply', {Id: this.{{Table.RealItemName}}.{{Table.PrimaryKeyField}}}, '{{endpoint}}');
        	this.{{Table.RealItemName}}.Done = false;
            ctrl.$emitGlobal('g.document.applied', this);
            ctrl.$requery();
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

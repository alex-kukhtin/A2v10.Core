// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class ScriptBuilder
{
    internal Task<String> CreateEditTemplate()
    {
        return Table.Kind switch
        {
            EndpointKind.Document => CreateDocumentTemplate(),
            _ => CreateGenericEditTemplate()
        };
    }

    private Task<String> CreateGenericEditTemplate()
    {
        IEnumerable<String> properties()
        {
            // one state property per collection - see TableMetadata.TabStateName
            foreach (var d in Table.Details.Values)
                yield return $$"""'{{Table.TypeName}}.{{d.TabStateName}}': {type: String, value: '{{d.FirstTabName}}'}""";
        }

        IEnumerable<String> validators()
        {
            var required = Endpoint.Declaration.Rules.Required.ToHashSet();
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

            foreach (var rs in Endpoint.Declaration.Details.Values.SelectMany(d => d.RowSets))
                foreach (var f in rs.Rules.Required)
                    yield return $"'{Table.Model}.{rs.Collection}[].{f}': `@[Error.Required]`";
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
        }

        const String jsDivider = ",\n\t\t";

        var templ = $$"""
        {{Imports(types(), "./edit")}}{{TemplateDecl}} {
            properties: {
                {{String.Join(jsDivider, properties())}}
            },
            validators: {
                {{String.Join(jsDivider, validators())}}
            },
        };

        {{TemplateExport}}

        {{String.Join('\n', functions())}}
        """;
        return Task.FromResult<String>(templ);
    }

    private Task<String> CreateDocumentTemplate()
    {
        IEnumerable<String> defaults()
        {
            if (Endpoint.Kind == EndpointKind.Operation)
            {
                var opColumn = Table.Columns.FirstOrDefault(c => c.Type == ColumnType.Operation);
                if (opColumn != null)
                    yield return $$"""'{{Table.Model}}.{{opColumn.Name}}'() { return { Id: '{{Endpoint.Name}}', Name: '{{Endpoint.Storage.Model}}'};}""";
            }
        }

        IEnumerable<String> properties()
        {
            // one state property per collection - see TableMetadata.TabStateName
            foreach (var d in Table.Details.Values)
                yield return $$"""'{{Table.TypeName}}.{{d.TabStateName}}': {type: String, value: '{{d.FirstTabName}}'}""";
            foreach (var (key, expr) in Endpoint.Declaration.Rules.Computed)
                yield return $$"""'{{Table.TypeName}}.{{key}}'({{Self(Table.TypeName)}}) { return {{expr}};}""";

            /* Computed lands on the TYPE, and a kind is its own type - which is why the types had
             * to be split before rules could speak per kind at all. 'Sum' computed in one kind
             * and entered in another is not two expressions, it is a getter here and data there,
             * and one type cannot be both. A total lands on the ARRAY of that type.
             */
            foreach (var rs in Endpoint.Declaration.Details.Values.SelectMany(d => d.RowSets))
            {
                foreach (var (key, expr) in rs.Rules.Computed)
                    yield return $$"""'{{rs.Type}}.{{key}}'({{Self(rs.Type)}}) { return {{expr}};}""";
                foreach (var name in rs.Rules.Total)
                    yield return $$"""'{{rs.Type}}Array.{{name}}'({{Self($"{rs.Type}Array")}}) { return this.$sum(c => c.{{name}}); }""";
            }
        }

        IEnumerable<String> validators()
        {
            foreach (var f in Endpoint.Declaration.Rules.Required)
                yield return $"'{Table.Model}.{f}': `@[Error.Required]`";

            // required lands on the PATH, and each row set has its own
            foreach (var rs in Endpoint.Declaration.Details.Values.SelectMany(d => d.RowSets))
                foreach (var f in rs.Rules.Required)
                    yield return $"'{Table.Model}.{rs.Collection}[].{f}': `@[Error.Required]`";
        }

        IEnumerable<String> events()
        {
            foreach (var (refName, inherits) in Endpoint.Declaration.Inherits)
            {
                var body = String.Join(" ", inherits.Select(x => $"doc.{x.Field.Name} = doc.{x.Ref.Name}.{x.Source};"));
                yield return $$"""'{{Table.Model}}.{{refName}}.change'(doc{{Ann(Table.TypeName)}}) { {{body}} }""";
            }

            // inherit lands on the PATH too - one handler per row set, not one per collection
            foreach (var rs in Endpoint.Declaration.Details.Values.SelectMany(d => d.RowSets))
                foreach (var (refName, inherits) in rs.Inherits)
                {
                    var body = String.Join(" ", inherits.Select(x => $"row.{x.Field.Name} = row.{x.Ref.Name}.{x.Source};"));
                    yield return $$"""'{{Table.Model}}.{{rs.Collection}}[].{{refName}}.change'(row{{Ann(rs.Type)}}) { {{body}} }""";
                }
        }

        IEnumerable<String> types()
        {
            yield return "TRoot";
            yield return Table.TypeName;
            foreach (var d in Table.Details.Select(x => x.Value))
            {
                yield return d.TypeName;
                yield return $"{d.TypeName}Array";
            }
        }

        const String jsDivider = ",\n\t\t";

        var endpoint = Endpoint.Path;
        var templ = $$"""
        {{Imports(types(), "./edit")}}{{TemplateDecl}} {
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

        {{TemplateExport}}

        async function post({{Self("TRoot")}}) {
            const ctrl{{Ann("IController")}} = this.$ctrl;
            await ctrl.$invoke('post', {Id: this.{{Table.Model}}.Id}, '{{endpoint}}');
        	this.{{Table.Model}}.Done = true;
            ctrl.$emitGlobal('g.document.posted', this);
            ctrl.$requery();
        }

        async function unPost({{Self("TRoot")}}) {
            const ctrl{{Ann("IController")}} = this.$ctrl;
            await ctrl.$invoke('unpost', {Id: this.{{Table.Model}}.Id}, '{{endpoint}}');
        	this.{{Table.Model}}.Done = false;
            ctrl.$emitGlobal('g.document.posted', this);
            ctrl.$requery();
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

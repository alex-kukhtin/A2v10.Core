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
            TableKind.Document => CreateDocumentTemplate(),
            _ => CreateGenericEditTemplate()
        };
    }

    /* One property per strip of the form this template serves, opening on the strip's first tab -
     * read off the FORM and not off the collections, because that is where strips exist. Driven
     * from the shape it declared a property for every collection: one too few for a form with two
     * strips over one collection, and one too many for every collection no strip shows.
     */
    private IEnumerable<String> TabStateProperties()
    {
        foreach (var strip in Endpoint.Declaration.Form(Constants.FormNames.Edit).TabStrips())
            yield return $$"""'{{Table.TypeName}}.{{strip.TabState}}': {type: String, value: '{{strip.Elements[0].RowSet}}'}""";
    }

    /* The query a picker sends - to its browse as the url's, to its fetch as arguments: every owner
     * of the target by the name it has THERE, valued from the field here (MetadataExtensions.OwnerLinks).
     * The id and not the element: the fetch reads its arguments as JSON, where an element would
     * arrive whole. An empty one is no filter on both roads - the url drops it, the fetch skips it.
     */
    private IEnumerable<String> OwnerProperties()
    {
        String query(TableColumn reference, String type, TableMetadata scope, TableMetadata? header)
        {
            var owners = reference.OwnerLinks(scope, header).Select(l =>
                $"{l.Owner.Name}: {(l.Header ? $"this.$root.{Table.Model}" : "this")}.{l.Field.ModelName}.$id");
            return $$"""'{{type}}.{{reference.OwnersName()}}'({{Self(type)}}) { return { {{String.Join(", ", owners)}} }; }""";
        }

        foreach (var column in Table.AllColumns(c => c.OwnerLinks(Table, null).Count > 0))
            yield return query(column, Table.TypeName, Table, null);

        // a property of the TYPE, and a kind is its own type
        foreach (var (collection, rows) in Table.Details)
            foreach (var column in rows.AllColumns(c => c.OwnerLinks(rows, Table).Count > 0))
                foreach (var rs in Endpoint.Declaration.Details[collection].RowSets)
                    yield return query(column, rs.Type, rows, Table);
    }

    /* A declared property lands on the TYPE, and a kind is its own type - which is why the types had
     * to be split before a property could differ per kind at all. 'Sum' computed in one kind and
     * entered in another is not two expressions, it is a getter here and data there, and one type
     * cannot be both. With a setter it is Vue's { get, set }.
     */
    private IEnumerable<String> DeclaredProperties()
    {
        String property(String type, String key, PropertyMetadata p) => p.Set == null
            ? $$"""'{{type}}.{{key}}'({{Self(type)}}) { return {{p.Get}}; }"""
            : $$"""'{{type}}.{{key}}': { get({{Self(type)}}) { return {{p.Get}}; }, set({{Self(type)}}{{(IsTs ? ", " : "")}}value{{Ann("any")}}) { {{p.Set}}; } }""";

        foreach (var (key, p) in Endpoint.Declaration.Properties)
            yield return property(Table.TypeName, key, p);
        foreach (var rs in Endpoint.Declaration.Details.Values.SelectMany(d => d.RowSets))
            foreach (var (key, p) in rs.Properties)
                yield return property(rs.Type, key, p);
    }

    /* 'required' of one scope, field by field. Unconditional, it is the platform's shorthand; only
     * under conditions, it is one rule whose 'when' holds if any of them does - 'required' unions, so
     * two conditions on one field are one requirement, and two rules would report it twice. The test
     * is the author's text with 'this' as the record, which is how the platform calls 'when'.
     */
    private Dictionary<String, String> RequiredRules(RuleMetadata rules, String type)
    {
        var result = rules.Required.ToDictionary(f => f, _ => "`@[Error.Required]`");
        var conditional = rules.When
            .SelectMany(w => w.Required.Select(f => (Field: f, w.Test)))
            .Where(x => !result.ContainsKey(x.Field))
            .GroupBy(x => x.Field);
        foreach (var g in conditional)
        {
            var test = g.Count() == 1 ? g.First().Test : String.Join(" || ", g.Select(x => $"({x.Test})"));
            result[g.Key] = $$$"""{valid: 'notBlank', msg: `@[Error.Required]`, when({{{Self(type)}}}) { return {{{test}}}; }}""";
        }
        return result;
    }

    private Task<String> CreateGenericEditTemplate()
    {
        IEnumerable<String> properties()
        {
            foreach (var state in TabStateProperties())
                yield return state;
            foreach (var p in DeclaredProperties())
                yield return p;
            foreach (var owners in OwnerProperties())
                yield return owners;
        }

        IEnumerable<String> events()
        {
            if (Table.HasTags)
                yield return """'g.tags.saved': tagsSaved""";
        }

        IEnumerable<String> validators()
        {
            var required = RequiredRules(Endpoint.Declaration.Rules, Table.TypeName);
            foreach (var col in Table.AllColumns(c => c.Unique || required.ContainsKey(c.Name)))
            {
                if (col.Unique && required.TryGetValue(col.Name, out var both))
                    yield return $$"""
                '{{Table.Model}}.{{col.Name}}': [
                    {{both}},
                    {valid: {{col.Name.ToLowerInvariant()}}Duplicate, async: true, msg: `@[Error.Duplicate]`}]
                """;
                else if (required.TryGetValue(col.Name, out var rule))
                    yield return $"'{Table.Model}.{col.Name}': {rule}";
                else if (col.Unique)
                    yield return $$"""'{{Table.Model}}.{{col.Name}}': {valid: {{col.Name.ToLowerInvariant()}}Duplicate, async: true, msg: `@[Error.{{Table.CollectionName}}.Duplicate.{{col.Name}}]`}""";
            }

            foreach (var rs in Endpoint.Declaration.Details.Values.SelectMany(d => d.RowSets))
                foreach (var (f, rule) in RequiredRules(rs.Rules, rs.Type))
                    yield return $"'{Table.Model}.{rs.Collection}[].{f}': {rule}";
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

            if (Table.HasTags)
            {
                var tags = Constants.FieldNames.Tags;
                yield return $$"""
                function tagsSaved(root) {
                	if (root.Params.For !== '{{Table.Model}}') return;
                	let tags = root.{{tags}};
                	this.Tags.$copy(tags);
                	this.{{Table.Model}}.{{tags}}.forEach(lt => {
                		let nt = tags.find(t => t.Id == lt.Id);
                		if (nt) lt.$merge(nt);
                	});
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
            events: {
                {{String.Join(jsDivider, events())}}
            }
        };

        {{TemplateExport}}

        {{String.Join('\n', functions())}}
        """;
        return Task.FromResult<String>(templ);
    }

    private Task<String> CreateDocumentTemplate()
    {
        IEnumerable<String> properties()
        {
            foreach (var state in TabStateProperties())
                yield return state;
            foreach (var p in DeclaredProperties())
                yield return p;

            // a total lands on the ARRAY of the row set's type
            foreach (var rs in Endpoint.Declaration.Details.Values.SelectMany(d => d.RowSets))
                foreach (var name in rs.Rules.Total)
                    yield return $$"""'{{rs.Type}}Array.{{name}}'({{Self($"{rs.Type}Array")}}) { return this.$sum(c => c.{{name}}); }""";
            foreach (var owners in OwnerProperties())
                yield return owners;
        }

        IEnumerable<String> validators()
        {
            foreach (var (f, rule) in RequiredRules(Endpoint.Declaration.Rules, Table.TypeName))
                yield return $"'{Table.Model}.{f}': {rule}";

            // required lands on the PATH, and each row set has its own
            foreach (var rs in Endpoint.Declaration.Details.Values.SelectMany(d => d.RowSets))
                foreach (var (f, rule) in RequiredRules(rs.Rules, rs.Type))
                    yield return $"'{Table.Model}.{rs.Collection}[].{f}': {rule}";
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

        // a kind is its own type (EditTSMap), so what is imported is the row sets' and not the collections'
        IEnumerable<String> types()
        {
            yield return "TRoot";
            yield return Table.TypeName;
            foreach (var rs in Endpoint.Declaration.Details.Values.SelectMany(d => d.RowSets))
            {
                yield return rs.Type;
                yield return $"{rs.Type}Array";
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

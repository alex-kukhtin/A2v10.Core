// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    private Task<String> CreateDocumentTemplate()
    {
        IEnumerable<String> defaults()
        {
            if (_table.Columns.Any(c => c.Name == "Date"))
                yield return $$"""'{{_table.RealItemName}}.Date'() { return du.today(); }""";
            if (_baseTable != null && _baseTable.IsOperation)
            {
                var opColumn = _table.Columns.FirstOrDefault(c => c.DataType == ColumnDataType.Operation);
                if (opColumn != null)
                    yield return $$"""'{{_table.RealItemName}}.{{opColumn.Name}}'() { return { Id: '{{_baseTable.Name.ToLowerInvariant()}}', Name: '{{_baseTable.RealItemName}}'};}""";
            }
        }

        IEnumerable<String> properties()
        {
            if (_table.Details.Count > 0)
            {
                var fd = _table.Details.First();
                if (fd.Kinds.Count == 0)
                    yield return $$"""'{{_table.RealTypeName}}.$$Tab': {type: String, value: '{{fd.Name}}'}""";
                else
                    yield return $$"""'{{_table.RealTypeName}}.$$Tab': {type: String, value: '{{fd.Kinds.First().Name}}'}""";
            }
            foreach (var c in _table.Columns.Where(c => !String.IsNullOrEmpty(c.Computed)))
                yield return $$"""'{{_table.RealTypeName}}.{{c.Name}}'() { return {{c.Computed}};}""";

            foreach (var d in _table.Details)
                foreach (var c in d.Columns.Where(c => !String.IsNullOrEmpty(c.Computed)))
                    yield return $$"""'{{d.RealTypeName}}.{{c.Name}}'() { return {{c.Computed}};}""";
        }

        IEnumerable<String> validators()
        {
            foreach (var col in _table.Columns.Where(c => c.Required))
                yield return $"'{_table.RealItemName}.{col.Name}': `@[Error.Required]`";
            if (_baseTable != null)
            {
                foreach (var d in _table.Details)
                    if (d.Kinds.Count > 0)
                        foreach (var k in d.Kinds)
                            foreach (var c in d.Columns.Where(c => c.Required))
                                yield return $"'{_table.RealItemName}.{k.Name}[].{c.Name}': `@[Error.Required]`";
                    else
                        foreach (var c in d.Columns.Where(c => c.Required))
                            yield return $"'{_table.RealItemName}.{d.RealItemsName}[].{c.Name}': `@[Error.Required]`";
            }
            else
                foreach (var d in _table.Details)
                    foreach (var c in d.Columns.Where(c => c.Required))
                        yield return $"'{_table.RealItemName}.{d.RealItemsName}[].{c.Name}': `@[Error.Required]`";
        }


        const String jsDivider = ",\n\t\t";

        var endpoint = _table.EndpointPathUseBase(_baseTable);
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
                apply,
                unApply
            }
        };

        module.exports = template;

        async function apply() {
            const ctrl = this.$ctrl;
            await ctrl.$invoke('apply', {Id: this.{{_table.RealItemName}}.{{_table.PrimaryKeyField}}}, '{{endpoint}}');
        	this.{{_table.RealItemName}}.Done = true;
            ctrl.$emitGlobal('g.document.applied', this);
            ctrl.$requery();
        }

        async function unApply() {
            const ctrl = this.$ctrl;
            await ctrl.$invoke('unapply', {Id: this.{{_table.RealItemName}}.{{_table.PrimaryKeyField}}}, '{{endpoint}}');
        	this.{{_table.RealItemName}}.Done = false;
            ctrl.$emitGlobal('g.document.applied', this);
            ctrl.$requery();
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

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
        }

        const String jsDivider = ",\n\t\t";

        var endpoint = _table.EndpointPathUseBase(_baseTable);
        var templ = $$"""
        const du = require('std:utils').date;
        const template = {
            properties: {
                 {{String.Join(jsDivider, properties())}},
            },
            defaults: {
                {{String.Join(jsDivider, defaults())}}
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
            ctrl.$requery();
        }

        async function unApply() {
            const ctrl = this.$ctrl;
            await ctrl.$invoke('unapply', {Id: this.{{_table.RealItemName}}.{{_table.PrimaryKeyField}}}, '{{endpoint}}');
            ctrl.$requery();
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    private Task<String> CreateDocumentTemplate()
    {
        var endpoint = $"/documents/{_table.RealItemsName}";
        var templ = $$"""
        const du = require('std:utils').date;
        const template = {
            properties: {
                'TRoot.$$Tab': String
            },
            defaults: {
                'Document.Date'() { return du.today(); }
            },
            commands: {
                apply,
                unApply
            }
        };

        module.exports = template;

        async function apply() {
            const ctrl = this.$ctrl;
            await ctrl.$invoke('apply', {Id: this.{{_table.RealItemName}}.{{_appMeta.IdField}}}, '{{endpoint}}');
            ctrl.$requery();
        }

        async function unApply() {
            const ctrl = this.$ctrl;
            await ctrl.$invoke('unapply', {Id: this.{{_table.RealItemName}}.{{_appMeta.IdField}}}, '{{endpoint}}');
            ctrl.$requery();
        }
        """;
        return Task.FromResult<String>(templ);
    }
}

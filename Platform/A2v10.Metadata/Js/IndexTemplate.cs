// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    private Task<String> CreateIndexTemplate()
    {
        var templ = $$"""
        const template = {
            options: {
                persistSelect: ['{{_table.RealItemsName}}']
            }
        };
        module.exports = template;
        """;
        return Task.FromResult<String>(templ);
    }
}

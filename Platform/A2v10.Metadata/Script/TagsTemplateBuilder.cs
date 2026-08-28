
using System;

namespace A2v10.Metadata;

internal class TagsTemplateBuilder
{
    internal String CreateIndexTemplate()
    {
        return $$"""
        const template = {
            options: {
                globalSaveEvent: 'g.tags.saved'
            },
            validators: {
                '{{Constants.FieldNames.Tags}}[].Name': `@[Error.Required]`
            },
        };
        
        module.exports = template;
        """;
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class ScriptBuilder
{
    // the card of a folder: a folder without a name is nothing the tree could show
    internal Task<String> CreateEditFolderTemplate()
    {
        var templ = $$"""
        {{TemplateDecl}} {
            validators: {
                '{{Constants.FieldNames.Folder}}.{{Constants.FieldNames.Name}}': `@[Error.Required]`
            }
        };

        {{TemplateExport}}
        """;
        return Task.FromResult<String>(templ);
    }
}

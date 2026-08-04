// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class JavascriptBuilder
{
    internal Task<String> CreateIndexTemplate()
    {
        var table = Endpoint.Storage;
        IEnumerable<String> events()
        {
            if (table.IsDocument) {
                yield return "'g.document.saved': handleSaved";
                yield return "'g.document.applied': handleApply";

            }
        }

        IEnumerable<String> options()
        {
            if (table.HasFolders)
                yield return $"persistSelect: ['Folders']";
            else
                yield return $"persistSelect: ['{table.CollectionName}']";
        }

        IEnumerable<String> functions()
        {
            if (table.IsDocument)
            {
                yield return $$"""
                function handleApply(elem) {
                    let doc = elem.{{table.Model}};
                    let found = this.{{table.CollectionName}}.find(d => d.Id == doc.Id);
                    if (!found) return;
                    found.Done = doc.Done;
                }
                """;

                yield return $$"""
                function handleSaved(elem) {
                    let doc = elem.{{table.Model}};
                    let found = this.{{table.CollectionName}}.$find(d => d.Id === doc.Id);
                    if (found)
                        found.$merge(doc).$select();
                }
                """;
            }
        }

        const String jsDivider = ",\n\t\t";

        var templ = $$"""
        const template = {
            options: {
                {{String.Join(jsDivider, options())}}
            },
            events: {
                {{String.Join(jsDivider, events())}}
            }
        };
        module.exports = template;

        {{String.Join("\n", functions())}}
        """;
        return Task.FromResult<String>(templ);
    }
}

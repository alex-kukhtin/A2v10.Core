// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
{
    internal Task<String> CreateIndexTemplate()
    {
        IEnumerable<String> events()
        {
            if (_table.IsDocument) {
                yield return "'g.document.saved': handleSaved";
                yield return "'g.document.applied': handleApply";

            }
        }

        IEnumerable<String> options()
        {
            if (_table.UseFolders)
                yield return $"persistSelect: ['Folders']";
            else
                yield return $"persistSelect: ['{_table.RealItemsName}']";
        }

        IEnumerable<String> functions()
        {
            if (_table.IsDocument)
            {
                yield return $$"""
                function handleApply(elem) {
                    let doc = elem.{{_table.RealItemName}};
                    let found = this.{{_table.RealItemsName}}.find(d => d.Id == doc.Id);
                    if (!found) return;
                    found.Done = doc.Done;
                }
                """;

                yield return $$"""
                function handleSaved(elem) {
                    let doc = elem.{{_table.RealItemName}};
                    let found = this.{{_table.RealItemsName}}.$find(d => d.Id === doc.Id);
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


    internal Task<String> CreateIndexTSTemplate()
    {
        IEnumerable<String> events()
        {
            if (_table.IsDocument)
            {
                yield return "'g.document.saved': handleSaved";
                yield return "'g.document.applied': handleApply";

            }
        }

        IEnumerable<String> options()
        {
            if (_table.UseFolders)
                yield return $"persistSelect: ['Folders']";
            else
                yield return $"persistSelect: ['{_table.RealItemsName}']";
        }

        IEnumerable<String> functions()
        {
            if (_table.IsDocument)
            {
                yield return $$"""
                function handleApply(elem: TRoot) {
                    let doc = elem.{{_table.RealItemName}};
                    let found = this.{{_table.RealItemsName}}.find(d => d.Id == doc.Id);
                    if (!found) return;
                    found.Done = doc.Done;
                }
                """;

                yield return $$"""
                function handleSaved(elem : TRoot) {
                    let doc = elem.{{_table.RealItemName}};
                    let found = this.{{_table.RealItemsName}}.$find(d => d.Id === doc.Id);
                    if (found)
                        found.$merge(doc).$select();
                }
                """;
            }
        }

        const String jsDivider = ",\n\t\t";

        var templ = $$"""

        const template: Template = {
            options: {
                {{String.Join(jsDivider, options())}}
            },
            events: {   
                {{String.Join(jsDivider, events())}}
            }
        };

        export default template;

        {{String.Join("\n", functions())}}
        """;
        return Task.FromResult<String>(templ);
    }
}

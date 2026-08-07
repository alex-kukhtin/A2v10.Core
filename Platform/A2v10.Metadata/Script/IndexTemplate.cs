// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class ScriptBuilder
{
    internal Task<String> CreateIndexTemplate()
    {
        IEnumerable<String> events()
        {
            if (Table.IsDocument)
            {
                yield return "'g.document.saved': handleSaved";
                // the name the document page emits - see CreateDocumentTemplate
                yield return "'g.document.posted': handlePosted";
            }
        }

        IEnumerable<String> options()
        {
            if (Table.HasFolders)
                yield return $"persistSelect: ['Folders']";
            else
                yield return $"persistSelect: ['{Table.CollectionName}']";
        }

        IEnumerable<String> functions()
        {
            if (Table.IsDocument)
            {
                yield return $$"""
                function handlePosted(elem{{Ann("TRoot")}}) {
                    let doc = elem.{{Table.Model}};
                    let found = this.{{Table.CollectionName}}.find(d => d.Id == doc.Id);
                    if (!found) return;
                    found.Done = doc.Done;
                }
                """;

                yield return $$"""
                function handleSaved(elem{{Ann("TRoot")}}) {
                    let doc = elem.{{Table.Model}};
                    let found = this.{{Table.CollectionName}}.$find(d => d.Id === doc.Id);
                    if (found)
                        found.$merge(doc).$select();
                }
                """;
            }
        }

        IEnumerable<String> types()
        {
            yield return "TRoot";
            yield return Table.TypeName;
            yield return $"{Table.TypeName}Array"; // collection type
            foreach (var r in Table.AllColumns().AllRefs())
                yield return r.Table.TypeName;
        }

        const String jsDivider = ",\n\t\t";

        var templ = $$"""
        {{Imports(types(), "./index")}}{{TemplateDecl}} {
            options: {
                {{String.Join(jsDivider, options())}}
            },
            events: {
                {{String.Join(jsDivider, events())}}
            }
        };

        {{TemplateExport}}

        {{String.Join("\n", functions())}}
        """;
        return Task.FromResult<String>(templ);
    }
}

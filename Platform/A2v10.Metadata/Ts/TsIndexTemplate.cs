// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class TypescriptBuilder
{
    internal Task<String> CreateIndexTSTemplate()
    {
        IEnumerable<String> events()
        {
            if (Table.IsDocument)
            {
                yield return "'g.document.saved': handleSaved";
                yield return "'g.document.applied': handleApply";

            }
        }

        IEnumerable<String> options()
        {
            if (Table.UseFolders)
                yield return $"persistSelect: ['Folders']";
            else
                yield return $"persistSelect: ['{Table.CollectionName}']";
        }

        IEnumerable<String> functions()
        {
            if (Table.IsDocument)
            {
                yield return $$"""
                function handleApply(elem: TRoot) {
                    let doc = elem.{{Table.Model}};
                    let found = this.{{Table.CollectionName}}.find(d => d.Id == doc.Id);
                    if (!found) return;
                    found.Done = doc.Done;
                }
                """;

                yield return $$"""
                function handleSaved(elem : TItemRoot) {
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


        var optionsList = options().ToList();
        var eventsList = events().ToList(); 

        IEnumerable<String> templateProps()
        {
            if (optionsList.Count > 0)
                yield return $$"""
                        options: {
                            {{String.Join(jsDivider, optionsList)}}
                        }
                    """;
            if (eventsList.Count > 0)
                yield return $$"""
                        events: {
                            {{String.Join(jsDivider, eventsList)}}
                        }
                    """;
        }

        var templ = $$"""

        import { {{String.Join(", ", types())}} } from './index';
        
        const template: Template = {
        {{String.Join(",\n", templateProps())}}
        };

        export default template;

        {{String.Join("\n", functions())}}
        """;
        return Task.FromResult<String>(templ);
    }
}

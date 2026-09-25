// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class ScriptBuilder
{
    // the root property the list's Create hands to the card as its query (XamlBuilder.ButtonCreate)
    internal const String CreateArgProperty = "$CreateArg";
    // the selected node when it is a folder, null on All and the root (XamlBuilder.FolderTree)
    internal const String SelectedFolderProperty = "$SelectedFolder";
    // the selected node when it is a place - a folder or the root - null on All (XamlBuilder.FolderTree)
    internal const String SelectedPlaceProperty = "$SelectedPlace";
    // the template command and the server's invoke share the name (BaseModelBuilder.InvokeAsync)
    internal const String DeleteFolderCommand = "deleteFolder";

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
            if (Table.HasTags)
                yield return "'g.tags.saved': tagsSaved";
        }

        IEnumerable<String> options()
        {
            if (Table.HasFolders)
                yield return $"persistSelect: ['Folders']";
            else
                yield return $"persistSelect: ['{Table.CollectionName}']";
        }

        /* What Create opens the card with (XamlBuilder.ButtonCreate): the selected folder, which the
         * card takes as InitialSource.Query. All and the root are no folder - either written into the
         * column would fail the foreign key - so the url gets nothing for them, and the element lands
         * in the root.
         */
        IEnumerable<String> properties()
        {
            if (Table.HasFolders)
            {
                yield return $"'TRoot.{SelectedPlaceProperty}': selectedPlace";
                yield return $"'TRoot.{SelectedFolderProperty}': selectedFolder";
                yield return $"'TRoot.{CreateArgProperty}': createArg";
            }
        }

        IEnumerable<String> functions()
        {
            if (Table.HasFolders)
            {
                var id = _descr.PlatformId;
                yield return $$"""
                function selectedPlace({{Self("TRoot")}}) {
                    const f = this.Folders.$selected;
                    return !f || String(f.Id) === '{{id.All}}' ? null : f;
                }

                function selectedFolder({{Self("TRoot")}}) {
                    const f = this.{{SelectedPlaceProperty}};
                    return !f || String(f.Id) === '{{id.Root}}' ? null : f;
                }

                function createArg({{Self("TRoot")}}) {
                    const f = this.{{SelectedFolderProperty}};
                    return f ? { {{Constants.FieldNames.Folder}}: f.Id } : {};
                }

                // a convenience: the server refuses a folder that is not empty whatever the page thinks
                function canDeleteFolder({{Self("TRoot")}}) {
                    const f = this.{{SelectedFolderProperty}};
                    return !!f && !f.SubItems.length;
                }

                async function deleteFolder({{Self("TRoot")}}) {
                    const f = this.{{SelectedFolderProperty}};
                    if (!f) return;
                    await this.$ctrl.$invoke('{{DeleteFolderCommand}}', { Id: f.Id });
                    f.$remove();
                }
                """;
            }
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
            if (Table.HasTags)
            {
                var tags = Constants.FieldNames.Tags;
                yield return $$"""
                    function tagsSaved(root) {
                    	if (root.Params.For !== '{{Table.Model}}') return;

                    	let tags = root.{{tags}};
                    	this.{{tags}}.$copy(tags);

                    	let ag = this.{{Table.CollectionName}};
                    	ag.forEach(ag => {
                    		ag.{{tags}}.forEach(at => {
                    			let nt = tags.find(tg => tg.Id == at.Id);
                    			if (nt) at.$merge(nt);
                    		});
                    	});
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
                yield return r.Table.RefTypeName;
        }

        IEnumerable<String> commands()
        {
            if (Table.HasFolders)
                yield return $$"""
                    {{DeleteFolderCommand}}: {
                        exec: deleteFolder,
                        canExec: canDeleteFolder,
                        confirm: `@[Confirm.Delete.Folder]`
                    }
                    """;
        }

        const String jsDivider = ",\n\t\t";

        var templ = $$"""
        {{Imports(types(), "./index")}}{{TemplateDecl}} {
            options: {
                {{String.Join(jsDivider, options())}}
            },
            properties: {
                {{String.Join(jsDivider, properties())}}
            },
            commands: {
                {{String.Join(jsDivider, commands())}}
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

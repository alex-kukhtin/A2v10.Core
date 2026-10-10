// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class ScriptBuilder
{
    /* The types of the index model. Exported, every one of them - the template imports what it
     * names, and a 'declare type' in a file that exports is local to it.
     */
    internal Task<String> CreateIndexMapTS()
    {
        var collType = $"{Table.TypeName}Array";
        var refDecl = String.Join("\n", RefTargets(withDetails: false).Select(RefTsInterface));

        var templ = Table.HasFolders
            ? $$"""

            {{refDecl}}
            export interface {{Table.TypeName}} extends IArrayElement {
            {{String.Join("\n", TsProperties(Table))}}
            }

            export type {{collType}} = IElementArray<{{Table.TypeName}}>;

            export interface TFolder extends ITreeElement {
                readonly Id: {{Table.KeyColumn.ToTsType(_descr.PlatformId)}};
                Icon: string;
                SubItems: TFolderArray;
                {{Table.CollectionName}}: {{collType}};
                InitExpand: boolean;
            }

            export type TFolderArray = IElementArray<TFolder>;

            /* The properties the template declares on the root (CreateIndexTemplate): the selected
             * place and folder, and what Create opens the card with.
             */
            export interface TRoot extends IRoot {
                readonly Folders: TFolderArray;
                readonly {{SelectedPlaceProperty}}: TFolder;
                readonly {{SelectedFolderProperty}}: TFolder;
                readonly {{CreateArgProperty}}: object;
            }
            """
            : $$"""

            {{refDecl}}
            export interface {{Table.TypeName}} extends IArrayElement {
            {{String.Join("\n", TsProperties(Table))}}
            }

            export type {{collType}} = IElementArray<{{Table.TypeName}}>;

            export interface TRoot extends IRoot {
                readonly {{Table.CollectionName}}: {{collType}};
            }
            """;
        return Task.FromResult(templ);
    }
}

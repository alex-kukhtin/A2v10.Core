// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class ScriptBuilder
{
    internal Task<String> CreateIndexMapTS()
    {
        var collType = $"{Table.TypeName}Array";
        var refDecl = String.Empty;

        var refs = Table.AllColumns().AllRefs().ToList();

        var refElems = refs.Select(x => $$"""
        export interface {{x.Table.RefTypeName}} extends IElement {
        {{String.Join("\n", TsProperties(x.Table))}}
        }
        """);

        if (refElems.Any())
            refDecl = $"\n{String.Join("\n", refElems)}\n";

        var templ = String.Empty;

        if (Table.HasFolders)
        {
            templ = $$"""

            {{refDecl}}
            export interface {{Table.TypeName}} extends IArrayElement {
            {{String.Join("\n", TsProperties(Table))}}
            }

            declare type {{collType}} = IElementArray<{{Table.TypeName}}>;

            export interface TFolder extends IArrayElement {
                readonly Id: number;
                Icon: string;
                SubItems: TFolderArray;
                {{Table.CollectionName}}: {{collType}};
                InitExpand: boolean;
            }

            declare type TFolderArray = IElementArray<TFolder>;
            
            export interface TRoot extends IRoot {
                readonly Folders: TFolderArray;
            }
            """;
        }
        else
        {
            templ = $$"""

            {{refDecl}}
            export interface {{Table.TypeName}} extends IArrayElement {
            {{String.Join("\n", TsProperties(Table))}}
            }

            declare type {{collType}} = IElementArray<{{Table.TypeName}}>;

            export interface TRoot extends IRoot {
                readonly {{Table.CollectionName}}: {{collType}};
            }
            """;
        }
        return Task.FromResult<String>(templ);
    }
}

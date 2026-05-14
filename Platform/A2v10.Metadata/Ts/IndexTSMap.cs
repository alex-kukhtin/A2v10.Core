// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class TypescriptBuilder
{
    internal Task<String> CreateIndexMapTS()
    {
        var collType = $"{Table.TypeName}Array";
        var refDecl = String.Empty;

        var refs = Table.AllColumns().AllRefs().ToList();

        // exclude self-references
        var refElems = refs.Where(x => x.Table.SqlTableName != Table.SqlTableName).Select(x => $$"""
        export interface {{x.Table.TypeName}} extends IElement {
        {{String.Join("\n", TsProperties(x.Table))}}
        }
        """);

        if (refElems.Any())
            refDecl = $"\n{String.Join("\n", refElems)}\n";

        var templ = String.Empty;

        if (Table.UseFolders)
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
                HasSubItems: boolean;
                {{Table.CollectionName}}: {{collType}};
                InitExpanded: boolean;
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

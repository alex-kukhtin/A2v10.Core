// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
{
    internal Task<String> CreateMapTS()
    {
        var collType = $"{_table.RealTypeName}Array";
        var refDecl = String.Empty;

        // exclude self-references
        var refElems = _refFields.RefTables().Where(x => x.SqlTableName != _table.SqlTableName).Select(x => $$"""
        export interface {{x.RealTypeName}} extends IElement {
        {{String.Join("\n", TsProperties(x))}}
        }
        """);

        if (refElems.Any())
            refDecl = $"\n{String.Join("\n", refElems)}\n";

        var templ = String.Empty;

        if (_table.UseFolders)
        {
            templ = $$"""

            {{refDecl}}
            export interface {{_table.RealTypeName}} extends IArrayElement {
            {{String.Join("\n", TsProperties(_table))}}
            }

            declare type {{collType}} = IElementArray<{{_table.RealTypeName}}>;

            export interface TFolder extends IArrayElement {
                readonly Id: {{_appMeta.IdDataType.ToTsType(_appMeta.IdDataType)}};
                Icon: string;
                SubItems: TFolderArray;
                HasSubItems: boolean;
                {{_table.RealItemsName}}: {{collType}};
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
            export interface {{_table.RealTypeName}} extends IArrayElement {
            {{String.Join("\n", TsProperties(_table))}}
            }

            declare type {{collType}} = IElementArray<{{_table.RealTypeName}}>;

            export interface TRoot extends IRoot {
                readonly {{_table.RealItemsName}}: {{collType}};
            }
            """;
        }
        return Task.FromResult<String>(templ);
    }
}

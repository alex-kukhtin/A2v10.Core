// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
{
    public Form CreateBrowseTreeDialog()
    {
        IEnumerable<FormItem> ToolbarButtons()
        {
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.Create)
                {
                    Url = $"{_table.EndpointPath()}/editfolder",
                    Argument = "Folders",
                }
            };
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.EditSelected)
                {
                    Url = $"{_table.EndpointPath()}/editfolder",
                    Argument = "Folders",
                }
            };
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.DeleteSelected)
                {
                    Url = $"{_table.EndpointPath()}/deletefolder",
                    Argument = "Folders",
                }
            };
            yield return new FormItem(FormItemIs.Separator);
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.Reload),
            };
        }

        return new Form()
        {
            Is = FormItemIs.Dialog,
            Data = "Folders",
            UseCollectionView = false,
            Schema = _table.Schema,
            Table = _table.Name,
            EditWith = _table.EditWith,
            Label = $"@Folder.Browse",
            Width = "40rem",
            Items = [
                new FormItem(FormItemIs.Grid) {
                    Items = [
                        new FormItem(FormItemIs.Toolbar) {
                            Items = [..ToolbarButtons()],
                            Grid = new FormItemGrid(1, 1)
                        },
                        new FormItem(FormItemIs.TreeView) {
                            Data = "Folders",
                            Height = "30rem",
                            Grid = new FormItemGrid(2, 1)
                        }
                    ],
                    Props = new FormItemProps()
                    {
                        Rows = "auto 1fr",
                    },
                }
            ],
            Buttons = [
                new FormItem(FormItemIs.Button)
                {
                    Label = "@Select",
                    Command = new FormItemCommand(FormCommand.Select, "Folders"),
                    Props = new FormItemProps()
                    {
                        Style = ItemStyle.Primary
                    }
                },
                new FormItem(FormItemIs.Button)
                {
                    Label = "@Cancel",
                    Command = new FormItemCommand(FormCommand.Close)
                }
            ]
        };
    }
}

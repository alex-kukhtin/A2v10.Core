// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    private Form CreateEditPage()
    {

        IEnumerable<FormItem> ToolbarButtons()
        {
            yield return new FormItem(FormItemIs.Button)
            {
                Label = "@SaveAndClose",
                Command = new FormItemCommand(FormCommand.SaveAndClose)
            };
            yield return new FormItem(FormItemIs.Button)
            {
                Label = "@Save",
                Command = new FormItemCommand(FormCommand.Save)
            };
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.Reload),
            };
            yield return new FormItem(FormItemIs.Aligner);
        }

        FormItem? CreateTaskPad()
        {
            return null;
        }

        return new Form()
        {
            Is = FormItemIs.Page,
            Schema = _table.Schema,
            Table = _table.Name,
            Data = _table.RealItemName,
            EditWith = _table.EditWith,
            Label = $"@{_table.RealItemsName}",
            Items = [
                new FormItem() {
                    Is = FormItemIs.Grid,
                    Props = new FormItemProps() {
                        Rows = "auto 1fr auto",
                        Columns = "1fr",
                    },
                    Height = "100%",
                    Items = [
                        new FormItem() {
                            Is = FormItemIs.Toolbar,
                            Grid = new FormItemGrid(1, 1),
                            Items = [..ToolbarButtons()]
                        }
                    ]
                }
            ],
            Taskpad = CreateTaskPad()
        };
    }
}

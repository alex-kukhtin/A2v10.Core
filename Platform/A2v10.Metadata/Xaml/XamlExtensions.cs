// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal static class XamlExtensions
{
    public static Icon Command2Icon(this FormCommand command)
    {
        return command switch 
        {
            FormCommand.Reload => Icon.Reload,
            FormCommand.Edit => Icon.Edit,
            FormCommand.Delete => Icon.Clear,
            FormCommand.Create => Icon.Plus,
            _ => Icon.NoIcon,
        };
    }

    public static Bind TypedBind(this FormItem item)
    {
        return item.DataType switch
        {
            ItemDataType.Currency => new Bind(item.Data) { DataType = DataType.Currency, HideZeros = true, NegativeRed = true },
            ItemDataType.Date => new Bind(item.Data) { DataType = DataType.Date },
            ItemDataType.DateTime => new Bind(item.Data) { DataType = DataType.DateTime },
            _ => new Bind(item.Data),
        };
    }

    public static TextAlign ToTextAlign(this ItemDataType dt)
    {
        return dt switch
        {
            ItemDataType.Currency => TextAlign.Right,
            _ => TextAlign.Default
        };
    }

    public static BindCmd BindCommand(this FormItem item)
    {
        BindCmd CreateDialogCommand(DialogAction action)
        {
            var cmd = new BindCmd()
            {
                Command = CommandType.Dialog,
                Action = action,
                Url = $"/{item.Parameter}/edit"
            };
            cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("Parent.ItemsSource"));
            return cmd;
        }

        BindCmd CreateSelectCommand()
        {
            var cmd = new BindCmd() 
            { 
                Command = CommandType.Select
            };
            cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(item.Parameter ?? String.Empty));
            return cmd;
        }

        return item.Command switch
        {
            FormCommand.Reload => new BindCmd() { Command = CommandType.Reload },
            FormCommand.Save => new BindCmd() { Command = CommandType.Save },
            FormCommand.SaveAndClose => new BindCmd() { Command = CommandType.SaveAndClose },
            FormCommand.Close => new BindCmd() { Command = CommandType.Close },
            FormCommand.Select => CreateSelectCommand(),
            FormCommand.Edit => CreateDialogCommand(DialogAction.EditSelected), 
            FormCommand.Create => CreateDialogCommand(DialogAction.Append),
            _ => throw new NotImplementedException($"Implement Command for {item.Command}")
        };

    }

    public static ColumnRole ToColumnRole(this ItemDataType dataType)
    {
        return dataType switch
        {
            ItemDataType.Id => ColumnRole.Id,
            ItemDataType.Boolean => ColumnRole.CheckBox,
            ItemDataType.Currency => ColumnRole.Number,
            ItemDataType.Date or ItemDataType.DateTime => ColumnRole.Date,
            _ => ColumnRole.Default,
        };
    }
    public static IEnumerable<DataGridColumn> IndexColumns(this FormOld form)
    {
        return form.Columns.Select(c =>
            new DataGridColumn()
            {
                Header = c.Header ?? $"@[{c.Path}]",
                Role = c.Role,
                LineClamp = c.Clamp,
                SortProperty = c.SortProperty,
                Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), new Bind(c.Path) { DataType = c.BindDataType })
            }
        );
    }
}

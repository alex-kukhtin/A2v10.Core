// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal static class XamlExtensions
{
    public static Icon Command2Icon(this FormItem item)
    {
        if (item.Command == null)
            return Icon.NoIcon;
        return item.Command.Command switch 
        {
            FormCommand.Reload => Icon.Reload,
            FormCommand.Create => Icon.Plus,
            FormCommand.Edit or FormCommand.Open => Icon.Edit,
            FormCommand.Delete => Icon.Clear,
            FormCommand.Copy => Icon.Copy,
            FormCommand.Apply => Icon.Apply,
            FormCommand.Unapply => Icon.Unapply,
            FormCommand.SaveAndClose => Icon.SaveCloseOutline,
            FormCommand.Save => Icon.SaveOutline,
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

    public static TextAlign ToTextAlign(this FormItem fi)
    {
        return fi.DataType switch
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
                Url = $"/{item.Command?.Url}/edit"
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
            cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(item.Command?.Argument ?? String.Empty));
            return cmd;
        }
        BindCmd CreateOpenCommand()
        {
            var cmd = new BindCmd()
            {
                Command = CommandType.OpenSelected,
                Url = $"/{item.Command?.Url}/edit"
            };
            cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(item.Command?.Argument ?? String.Empty));
            return cmd;
        }

        return item.Command?.Command switch
        {
            FormCommand.Reload => new BindCmd() { Command = CommandType.Reload },
            FormCommand.Save => new BindCmd() { Command = CommandType.Save },
            FormCommand.SaveAndClose => new BindCmd() { Command = CommandType.SaveAndClose },
            FormCommand.Close => new BindCmd() { Command = CommandType.Close },
            FormCommand.Select => CreateSelectCommand(),
            FormCommand.Edit => CreateDialogCommand(DialogAction.EditSelected), 
            FormCommand.Create => CreateDialogCommand(DialogAction.Append),
            FormCommand.Open => CreateOpenCommand(),
            _ => throw new NotImplementedException($"Implement Command for {item.Command?.Command}")
        };

    }

    public static ColumnRole ToColumnRole(this FormItem item)
    {
        return item.DataType switch
        {
            ItemDataType.Id => ColumnRole.Id,
            ItemDataType.Boolean => ColumnRole.CheckBox,
            ItemDataType.Currency => ColumnRole.Number,
            ItemDataType.Date or ItemDataType.DateTime => ColumnRole.Date,
            _ => ColumnRole.Default,
        };
    }

    public static String? Localize(this String? source)
    {
        if (source == null) 
            return null;
        if (source.StartsWith("@"))
            return $"@[{source[1..]}]";
        return source;   
    }
}

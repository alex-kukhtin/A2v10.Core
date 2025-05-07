// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal static class XamlExtensions
{
    public static RenderMode? Command2RenderMode(this FormItem item)
    {
        return item.Command?.Command switch
        {
            FormCommand.Dialog => RenderMode.Show,
            _ => null,
        };
    }

    public static Icon Command2Icon(this FormItem item)
    {
        if (item.Command == null)
            return Icon.NoIcon;
        return item.Command.Command switch 
        {
            FormCommand.Reload => Icon.Reload,
            FormCommand.Create => Icon.Plus,
            FormCommand.EditSelected or FormCommand.Edit => Icon.Edit,
            FormCommand.Delete or FormCommand.DeleteSelected => Icon.Clear,
            FormCommand.Copy => Icon.Copy,
            FormCommand.Apply => Icon.Apply,
            FormCommand.UnApply => Icon.Unapply,
            FormCommand.SaveAndClose => Icon.SaveCloseOutline,
            FormCommand.Save => Icon.SaveOutline,
            FormCommand.Print => Icon.Print,
            FormCommand.Append => Icon.Plus,
            FormCommand.Dialog => Icon.Account,
            _ => Icon.NoIcon,
        };
    }

    public static Bind TypedBind(this FormItem item)
    {
        return item.DataType switch
        {
            ItemDataType.Currency => new Bind(item.Data) { DataType = DataType.Currency, HideZeros = true, NegativeRed = true },
            ItemDataType.Number => new Bind(item.Data) { DataType = DataType.Number, HideZeros = true, NegativeRed = true },
            ItemDataType.Date => new Bind(item.Data) { DataType = DataType.Date },
            ItemDataType.DateTime => new Bind(item.Data) { DataType = DataType.DateTime },
            _ => new Bind(item.Data),
        };
    }

    public static TextAlign ToTextAlign(this FormItem fi)
    {
        return fi.DataType switch
        {
            ItemDataType.Currency or ItemDataType.Number => TextAlign.Right,
            ItemDataType.Date or ItemDataType.DateTime => TextAlign.Center,
            _ => TextAlign.Default
        };
    }

    public static BindCmd BindCommandArg(this FormItem item, CommandType commandType)
    {
        var cmd = new BindCmd() { Command = commandType };
        cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(item.Command?.Argument ?? String.Empty));
        return cmd;
    }

    public static BindCmd BindCommand(this FormItem item, EditWithMode mode)
    {
        BindCmd CreateCreateCommand()
        {
            if (mode == EditWithMode.Dialog)
            {
                var cmd = new BindCmd()
                {
                    Command = CommandType.Dialog,
                    Action = DialogAction.Append,
                    Url = $"{item.Command?.Url}/edit",
                };
                cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("Parent.ItemsSource"));
                return cmd;
            }
            else
            {
                return new BindCmd()
                {
                    Command = CommandType.Open,
                    Url = $"{item.Command?.Url}/edit",
                    Argument = "new",
                };
            }
        }

        BindCmd EditSelectedCommand()
        {
            var url = item.Command.Url
                ?? throw new InvalidOperationException("Url is required for EditSelected Command");
            var urlBind = url.StartsWith("{");
            var cmd = new BindCmd()
            {
                Command = CommandType.OpenSelected,
                Url = urlBind ? null : $"{item.Command?.Url}"
            };
            if (mode == EditWithMode.Dialog)
            {
                cmd.Command = CommandType.Dialog;
                cmd.Action = DialogAction.EditSelected;
            }
            cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(item.Command?.Argument ?? String.Empty));
            if (urlBind)
                cmd.BindImpl.SetBinding(nameof(BindCmd.Url), new Bind(url[1..^1]));
            return cmd;
        }

        BindCmd DeleteSelectedCommand()
        {
            var cmd = new BindCmd()
            {
                Command = CommandType.DbRemoveSelected,
                Confirm = new Confirm() { Message = "@[Confirm.Delete.Element]" },
            };
            cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(item.Command?.Argument ?? String.Empty));
            return cmd;
        }


        BindCmd ShowDialogCommand()
        {
            var cmd = new BindCmd()
            {
                Command = CommandType.Dialog,
                Action = DialogAction.Show,
                Url = item.Command?.Url
            };
            if (!String.IsNullOrEmpty(item.Command?.Argument))
                cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(item.Command.Argument));
            return cmd;
         }


        return item.Command?.Command switch
        {
            FormCommand.Reload => new BindCmd() { Command = CommandType.Reload },
            FormCommand.Save => new BindCmd() { Command = CommandType.Save },
            FormCommand.SaveAndClose => new BindCmd() { Command = CommandType.SaveAndClose },
            FormCommand.Close => new BindCmd() { Command = CommandType.Close },
            FormCommand.Select => item.BindCommandArg(CommandType.Select),
            FormCommand.EditSelected => EditSelectedCommand(),
            FormCommand.DeleteSelected => DeleteSelectedCommand(),
            FormCommand.Create => CreateCreateCommand(),
            FormCommand.Append => item.BindCommandArg(CommandType.Append),
            FormCommand.Apply => new BindCmd()
            {
                Command = CommandType.Execute,
                CommandName = "apply",
                SaveRequired = true,
                ValidRequired = true
            },
            FormCommand.UnApply => new BindCmd()
            {
                Command = CommandType.Execute,
                CommandName = "unApply"
            },
            FormCommand.Open => new BindCmd()
            {
                Command = CommandType.Open,
                Url = item.Command?.Url
            },
            FormCommand.Print => new BindCmd()
            {
                Command = CommandType.Print
            },
            FormCommand.Dialog => ShowDialogCommand(),            
            FormCommand.Remove => item.BindCommandArg(CommandType.Remove),
            _ => throw new NotImplementedException($"Implement Command for {item.Command?.Command}")
        };

    }

    public static Boolean IsCheckedColumn(this FormItem item)
    {
        return item.Data == "$checked" && item.DataType == ItemDataType.Boolean;
    }

    public static ColumnRole ToColumnRole(this FormItem item)
    {
        return item.DataType switch
        {
            ItemDataType.Id => ColumnRole.Id,
            ItemDataType.Boolean => ColumnRole.CheckBox,
            ItemDataType.Currency or ItemDataType.Number => ColumnRole.Number,
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
        return source.Replace("\"", "&quot;");
    }

    public static String BindText(this FormItem item)
    {
        return $$"""{Bind {{item.Data}}}""";
    }
}

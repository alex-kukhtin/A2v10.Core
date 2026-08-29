// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    UIElementBase ToolbarControl(CommandBarItem cmd)
    {
        return cmd.Kind switch
        {
            CommandBarItemKind.Separator => new Separator(),
            CommandBarItemKind.Aligner => new ToolbarAligner(),
            CommandBarItemKind.Command => CommandBarControl(cmd.Command!.Value),
            _ => throw new InvalidOperationException($"Invalid enum {cmd.Kind}")
        };
    }

    UIElementBase CommandBarControl(EntityCommandType cmd)
    {
        return cmd switch
        {
            EntityCommandType.Reload => new Button()
            {
                Icon = Icon.Reload,
                Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.Reload))
            },
            EntityCommandType.Search => new SearchBox()
            {
                TabIndex = 1,
                Placeholder = "@[Search]",
                Bindings = b => b.SetBinding(nameof(SearchBox.Value), new Bind("Parent.Filter.Fragment"))
            },
            EntityCommandType.Save => new Button()
            {
                Icon = Icon.SaveOutline,
                Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.Save))
            },
            EntityCommandType.SaveAndClose => new Button()
            {
                Icon = Icon.SaveCloseOutline,
                Content = "@[SaveAndClose]",
                Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.SaveAndClose))
            },
            EntityCommandType.Edit => ButtonEditSelected(),
            EntityCommandType.Create => ButtonCreate(),
            EntityCommandType.Delete => new Button() 
            { 
                Icon = Icon.Clear,
                Bindings = b =>
                {
                    var cmd = new BindCmd()
                    {
                        Command = CommandType.DbRemoveSelected,
                        Confirm = new Confirm() { Message = "@[Confirm.Delete]" }
                    };
                    cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("Parent.ItemsSource"));
                    b.SetBinding(nameof(Button.Command), cmd);
                }
            },
            EntityCommandType.Show => new Button()
            {
                Icon = Icon.ArrowOpen,
                Content = "@[Show]",
                Bindings = b =>
                {
                    var cmd = new BindCmd()
                    {
                        Command = CommandType.OpenSelected,
                        Url = $"{Endpoint.Path}/show"
                    };
                    cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("Parent.ItemsSource"));
                    b.SetBinding(nameof(Button.Command), cmd);
                }
            },
            EntityCommandType.Print => new Button() { Icon = Icon.Print, Content = "@[Print]", Render=RenderMode.Show },
            EntityCommandType.Attachments => new Button() { Icon = Icon.Attach, Render=RenderMode.Show },
            EntityCommandType.Copy => new Button() { Icon = Icon.Copy },
            EntityCommandType.Post => new Button() 
                { 
                    Icon = Icon.Apply, 
                    Content = "@[Post]",
                    Bindings = b => {
                        var cmd = new BindCmd(CommandType.Execute)
                        {
                            CommandName = "post",
                            ValidRequired = true,
                            SaveRequired = true,
                            Confirm = new Confirm() { Message = "@[Confirm.Document.Post]"}
                        };
                        cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind($"{Table.Model}"));
                        b.SetBinding(nameof(Button.Command), cmd);
                        b.SetBinding(nameof(Button.If), new Bind($"!{Table.Model}.Done"));
                    }
                },
            EntityCommandType.UnPost => new Button() 
                { 
                    Icon = Icon.Unapply, 
                    Content = "@[UnPost]",
                    Bindings = b => {
                        b.SetBinding(nameof(Button.If), new Bind($"{Table.Model}.Done"));
                        var cmd = new BindCmd(CommandType.Execute)
                        {
                            CommandName = "unPost",
                            Confirm = new Confirm() { Message = "@[Confirm.Document.UnPost]" },
                            CheckReadOnly = false
                        };
                        cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind($"{Table.Model}"));
                        b.SetBinding(nameof(Button.Command), cmd);
                    }
            },
        EntityCommandType.ShowTrans => new Button
            {
                Icon = Icon.Apply,
                Content = "@[Transactions]",
                Render = RenderMode.Show,
                Bindings = b =>
                {
                    b.SetBinding(nameof(Button.If), new Bind($"{Table.Model}.Done"));
                    var cmd = new BindCmd(CommandType.Dialog)
                    {
                        Action = DialogAction.Show,
                        Url = $"{Endpoint.Path}/{Constants.Trans.Action}",
                    };
                    cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind($"{Table.Model}"));
                    b.SetBinding(nameof(Button.Command), cmd);
                }
        },
        _ => throw new InvalidOperationException($"Invalid CommandType {cmd}")

        };
    }

    Button ButtonCreate()
    {
        var bindCmd = new BindCmd()
        {
            Url = $"{Endpoint.Path}/edit"
        };
        if (Table.EditWithPage)
        {
            bindCmd.Command = CommandType.Open;
            bindCmd.Argument = "new";
        }
        else
        {
            bindCmd.Command = CommandType.Dialog;
            bindCmd.Action = DialogAction.Append;
            bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(Table.CollectionName));
        }

        return new Button()
        {
            Icon = Icon.Add,
            Content = "@[Create]",
            Bindings = b => b.SetBinding(nameof(Button.Command), bindCmd)
        };
    }

    Button ButtonEditSelected()
    {
        var bindCmd = new BindCmd()
        {
            Url = $"{Endpoint.Path}/edit"
        };
        bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(Table.CollectionName));
        if (Table.EditWithPage)
        {
            bindCmd.Command = CommandType.OpenSelected;
        }
        else
        {
            bindCmd.Command = CommandType.Dialog;
            bindCmd.Action = DialogAction.EditSelected;
        }
        return new Button()
        {
            Icon = Icon.Edit,
            Tip = "@[Edit]",
            Bindings = b => b.SetBinding(nameof(Button.Command), bindCmd)
        };
    }
}

// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;
using XMenuItem = A2v10.Xaml.MenuItem;

namespace A2v10.Metadata;

/* Which screen a toolbar is being built for. Passed in, never looked up: a command whose rendering
 * depends on where it sits would otherwise have to reach for the current action, which is not in
 * its signature - and a button that draws differently depending on who called it is the hardest
 * kind of drift to see. Two call sites, both of which know the answer statically.
 *
 * NOT a second EntityCommandType: printing from a card and from a grid is one act with two argument
 * sources, and the entity's command namespace answers what it can DO. See CLAUDE.md, "Commands".
 */
internal enum CommandScope
{
    Record,
    Grid
}

internal partial class XamlBuilder
{
    UIElementBase ToolbarControl(CommandBarItem cmd, CommandScope scope)
    {
        return cmd.Kind switch
        {
            CommandBarItemKind.Separator => new Separator(),
            CommandBarItemKind.Aligner => new ToolbarAligner(),
            CommandBarItemKind.Command => CommandBarControl(cmd.Command!.Value, scope),
            _ => throw new InvalidOperationException($"Invalid enum {cmd.Kind}")
        };
    }

    UIElementBase CommandBarControl(EntityCommandType cmd, CommandScope scope)
    {
        return cmd switch
        {
            EntityCommandType.Reload => FormButtons.Reload,
            EntityCommandType.Search => new SearchBox()
            {
                TabIndex = 1,
                Placeholder = "@[Search]",
                Bindings = b => b.SetBinding(nameof(SearchBox.Value), new Bind("Parent.Filter.Fragment"))
            },
            EntityCommandType.Save => FormButtons.Save,
            EntityCommandType.SaveAndClose => FormButtons.SaveAndClose,
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
            EntityCommandType.Print => ButtonPrint(scope),
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

    /* One item per declared blank. The list is never empty here: the button exists only because
     * there was one to print (DefaultFormBuilder.PrintCommand), so an empty menu has no spelling
     * rather than being guarded against.
     */
    Button ButtonPrint(CommandScope scope) => new()
    {
        Icon = Icon.Print,
        Content = "@[Print]",
        Render = RenderMode.Show,
        DropDown = new DropDownMenu()
        {
            Children = [.. Declaration.PrintForms.Select(pf => PrintMenuItem(pf, scope))]
        }
    };

    /* '<endpoint>/print/{0}?Form=<name>'. The '{0}' is where the command puts the id, and it has to
     * be written: without it the id is appended to the END of the string, past the '?', and the
     * platform then reads the action out of the wrong segment. A route with a query must say where
     * its id belongs.
     *
     * The name comes from PrintFormMetadata, which is also what the loader resolves '?Form='
     * against, so the address written here and the blank opened there cannot drift.
     *
     * 'Open' takes the record the card is showing; 'OpenSelected' takes the row the grid has. One
     * command either way - the screen is a parameter, not a second name.
     *
     * Aliased: A2v10.Metadata has a MenuItem of its own - the application menu tree.
     */
    XMenuItem PrintMenuItem(PrintFormMetadata form, CommandScope scope) => new()
    {
        Content = form.Title,
        Bindings = b =>
        {
            var grid = scope == CommandScope.Grid;
            var cmd = new BindCmd(grid ? CommandType.OpenSelected : CommandType.Open)
            {
                SaveRequired = true,
                Url = $"{Endpoint.Path}/{Constants.Print.Action}/{{0}}?{Constants.Print.FormQuery}={form.Name}",
            };
            cmd.BindImpl.SetBinding(nameof(BindCmd.Argument),
                new Bind(grid ? "Parent.ItemsSource" : Table.Model));
            b.SetBinding(nameof(XMenuItem.Command), cmd);
        }
    };

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

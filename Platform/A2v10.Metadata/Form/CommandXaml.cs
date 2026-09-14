// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
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
    /* The standard bar of a screen: derived, never declared - which commands exist is answered by
     * the kind, 'post', 'printForms' and traits, and no form takes one away. The form's 'toolbar'
     * slot adds its own in ONE place, between the entity's commands and the chrome tail, behind a
     * separator of its own. Where that place is belongs to the template, so the author has no
     * alignment token to write. See CLAUDE.md, "Commands".
     */
    Toolbar StandardToolbar(IReadOnlyList<CommandBarItem> entity, FormElement slot,
        IEnumerable<UIElementBase> chrome, CommandScope scope)
    {
        IEnumerable<CommandBarItem> own = slot.Commands.Count == 0 || entity.Count == 0
            ? slot.Commands
            : [CommandBarItem.Separator, .. slot.Commands];
        return new Toolbar(_xamlServiceProvider)
        {
            Children = [.. entity.Concat(own).Select(c => ToolbarControl(c, scope)), .. chrome]
        };
    }

    // the tail of a grid screen: reload, and the search pushed to the right edge
    IEnumerable<UIElementBase> GridChrome() =>
        [new Separator(), FormButtons.Reload, new ToolbarAligner(), SearchControl()];

    /* Print exists when the endpoint declares a blank to print, and it carries its own leading
     * separator so that it leaves without doubling the next one - the posting group is the same
     * shape. ONE command on both bars: printing is one act, and that the card hands it the record
     * while the grid hands it the selected row is a property of the screen - see CommandScope.
     */
    List<CommandBarItem> PrintCommand() =>
        Declaration.PrintForms.Count > 0
            ? [CommandBarItem.Separator, EntityCommandType.Print]
            : [];

    Toolbar IndexToolbar(FormElement slot) => Table.Kind switch
    {
        EndpointKind.Catalog => StandardToolbar(
            [EntityCommandType.Create, EntityCommandType.Edit, EntityCommandType.Delete,
                CommandBarItem.Separator, EntityCommandType.Show],
            slot, GridChrome(), CommandScope.Grid),
        EndpointKind.Document => StandardToolbar(
            [EntityCommandType.Create, EntityCommandType.Edit, EntityCommandType.Delete, .. PrintCommand()],
            slot, GridChrome(), CommandScope.Grid),
        EndpointKind.Journal => StandardToolbar([EntityCommandType.Edit], slot, GridChrome(), CommandScope.Grid),
        EndpointKind.Operation => StandardToolbar([], slot, [], CommandScope.Grid),
        _ => throw new InvalidOperationException($"No standard commands for {Table.Schema}")
    };

    Toolbar BrowseToolbar(FormElement slot) =>
        StandardToolbar([EntityCommandType.Create, EntityCommandType.Edit, EntityCommandType.Delete],
            slot, GridChrome(), CommandScope.Grid);

    Toolbar EditToolbar(FormElement slot)
    {
        IEnumerable<CommandBarItem> entity()
        {
            yield return EntityCommandType.SaveAndClose;
            yield return EntityCommandType.Save;
            foreach (var p in PrintCommand())
                yield return p;
            /* The whole posting group or none of it - including its leading separator, which
             * otherwise doubles up with the next one. A document whose endpoint declares no 'post'
             * has no Post, no UnPost and nothing to show in the transactions dialog.
             */
            if (Declaration.Post is { Count: > 0 })
            {
                yield return CommandBarItem.Separator;
                yield return EntityCommandType.Post;
                yield return EntityCommandType.UnPost;
                yield return EntityCommandType.ShowTrans;
            }
        }
        IEnumerable<CommandBarItem> chrome()
        {
            yield return CommandBarItem.Separator;
            if (Table.Traits.Contains(TableTrait.Attachments))
            {
                yield return EntityCommandType.Attachments;
                yield return CommandBarItem.Separator;
            }
            yield return EntityCommandType.Reload;
        }
        return StandardToolbar([.. entity()], slot,
            chrome().Select(c => ToolbarControl(c, CommandScope.Record)), CommandScope.Record);
    }

    UIElementBase ToolbarControl(CommandBarItem cmd, CommandScope scope)
    {
        return cmd.Kind switch
        {
            CommandBarItemKind.Separator => new Separator(),
            CommandBarItemKind.Command => CommandBarControl(cmd.Command!.Value, scope),
            _ => throw new InvalidOperationException($"Invalid enum {cmd.Kind}")
        };
    }

    SearchBox SearchControl() => new()
    {
        TabIndex = 1,
        Placeholder = "@[Search]",
        Bindings = b => b.SetBinding(nameof(SearchBox.Value), new Bind("Parent.Filter.Fragment"))
    };

    UIElementBase CommandBarControl(EntityCommandType cmd, CommandScope scope)
    {
        var elemOrDoc = Table.IsDocument ? "Document" : "Element";

        return cmd switch
        {
            EntityCommandType.Reload => FormButtons.Reload,
            EntityCommandType.Search => SearchControl(),
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
                        Confirm = new Confirm() { Message = $"@[Confirm.Delete.{elemOrDoc}]" }
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

    /* One item per declared blank. The list is never empty on the standard bar: the button exists
     * there only because there was one to print (PrintCommand), so an empty menu has no spelling
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

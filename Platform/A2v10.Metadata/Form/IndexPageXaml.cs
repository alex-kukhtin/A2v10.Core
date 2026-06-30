// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    IEnumerable<DataGridColumn> IndexColumnsXaml(Dictionary<String, FormColumn> columns, Boolean hasChecked) =>
        columns.Select(col =>
            new DataGridColumn()
            {
                Header = col.Value.Header,
                Role = col.Value.DataType.ToXamlColumnRole(),
                SortProperty = col.Value.DataType == FormColumnType.Ref ? col.Key : null,
                Bindings = b => b.SetBinding(nameof(DataGridColumn.Content),
                    new Bind(col.Value.Path) { DataType = col.Value.DataType.ToXamlDataType() })
            }
         );

    IEnumerable<FilterItem> CollectionViewFilters()
    {
        yield return new FilterItem()
        {
            Property = "Fragment",
            DataType = DataType.String
        };
        foreach (var f in Table.TableFilters())
            yield return f.Type switch
            {
                FormFilterType.Period => new FilterItem() { Property = "Period", DataType = DataType.Period },
                _ => new FilterItem() { Property = f.Column, DataType = DataType.Object }
            };
    }

    CollectionView XamlCollectionView() =>
        new()
        {
            RunAt = RunMode.Server,
            Bindings = b => b.SetBinding(nameof(CollectionView.ItemsSource), new Bind(Table.CollectionName)),
            Filter = new FilterDescription()
            {
                Items = [.. CollectionViewFilters()]
            }
        };

    UIElementBase CommandBarControl(EntityCommandType cmd)
    {
        return cmd switch
        {
            EntityCommandType.Reload => new Button()
            {
                Icon = Icon.Reload,
                Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(nameof(CommandType.Reload)))
            },
            EntityCommandType.Search => new SearchBox()
            {
                TabIndex = 1,
                Placeholder = "@[Search]",
                Bindings = b => b.SetBinding(nameof(SearchBox.Value), new Bind("Parent.Filter.Fragment"))
            },
            EntityCommandType.Edit => ButtonEditSelected(),
            EntityCommandType.Add => ButtonCreate(),
            EntityCommandType.Delete => new Button() { Icon = Icon.Clear },
            EntityCommandType.Show => new Button() { Icon = Icon.ArrowOpen, Content="@[Show]" },
            _ => throw new InvalidOperationException($"Invalid CommandType {cmd}")

        };
    }

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

    Button ButtonCreate()
    {
        BindCmd CreateBindCmd()
        {
            var bindCmd = new BindCmd();
            if (Table.EditWith == EditWithMode.Dialog)
            {
                bindCmd.Command = CommandType.Dialog;
                bindCmd.Action = DialogAction.Append;
                bindCmd.Url = $"{Table.Path}/edit";
                bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(Table.CollectionName));
            }
            return bindCmd;
        }

        return new Button()
        {
            Icon = Icon.Add,
            Content = "@[Create]",
            Bindings = b => b.SetBinding(nameof(Button.Command), CreateBindCmd())
        };
    }

    Button ButtonEditSelected()
    {
        BindCmd CreateBindCmd()
        {
            var bindCmd = new BindCmd();
            if (Table.EditWith == EditWithMode.Dialog)
            {
                bindCmd.Command = CommandType.Dialog;
                bindCmd.Action = DialogAction.EditSelected;
                bindCmd.Url = $"{Table.Path}/edit";
                bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(Table.CollectionName));
            }
            return bindCmd;
        }

        return new Button()
        {
            Icon = Icon.Edit,
            Tip = "@[Edit]",
            Bindings = b => b.SetBinding(nameof(Button.Command), CreateBindCmd())
        };
    }

    internal UIElement CreateXamlContainer(String action)
    {
        return action switch
        {
            "index" => CreateIndexPageXaml(Table.IndexForm()),
            "browse" => CreateBrowseDialogXaml(Table.BrowseForm()),
            "edit" => CreateEditDialogXaml(Table.EditForm()),
            _ => throw new InvalidOperationException($"Invalid action: '{action}'")
        };
    }

    internal Page CreateIndexPageXaml(FormMetadata meta)
    {
        return new Page()
        {
            CollectionView = XamlCollectionView(),
            Children = [IndexPageGrid(meta)],
            Taskpad = IndexTaskpad(meta.TaskPad)
        };
    }

    internal Partial CreateIndexPagePartialXaml()
    {
        var form = Table.IndexForm();
        var collView = XamlCollectionView();
        collView.Children = [IndexPageGrid(form)];
        return new Partial()
        {
            Children = [collView]
        };
    }

    UIElementBase ElementToControl(FormElement elem)
    {
        return elem switch
        {
            FormToolbar tb => new Toolbar(_xamlServiceProvider)
            {
                Children = [.. tb.Commands.Select(ToolbarControl)]
            },
            FormDataGrid dg => new DataGrid()
            {
                FixedHeader = true,
                Sort = true,
                Bindings = b =>
                {
                    b.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource"));
                },
                Columns = [.. IndexColumnsXaml(dg.Columns, false)]
            },
            FormPager pg => new Pager()
            {
                Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))
            },
            FormGrid fg => new Grid(_xamlServiceProvider)
            {
                Children = [..fg.Columns.Select(x => CreateEditControl(x.Value))]
            },
            _ => throw new InvalidOperationException($"Invalid control {elem}")
        };
    }
    internal Grid IndexPageGrid(FormMetadata meta)
    {
        return new Grid(_xamlServiceProvider)
        {
            Rows = RowDefinitions.FromString("Auto,1*,Auto"),
            Height = Length.FromString("100%"),
            Children = [..meta.Elements.Select(ElementToControl)]
        };
    }

    UIElement CreateFilterControl(FormFilter filter)
    {
        var elem = Table.AllColumns(x => x.Name == filter.Column).FirstOrDefault();
        if (elem == null)
            return new Block();
        return filter.Type switch
        {
            FormFilterType.Period =>
                new PeriodPicker()
                {
                    Label = $"@[Period]",
                    Placement = DropDownPlacement.BottomRight,
                    Display = DisplayMode.Name,
                    Bindings = b =>
                    {
                        b.SetBinding(nameof(PeriodPicker.Value), new Bind("Parent.Filter.Period"));
                        b.SetBinding(nameof(PeriodPicker.Description), new Bind("Parent.Filter.Period.Name"));
                    }
                },
            _ => new SelectorSimple()
            {
                Label = $"@[{elem.RefTableCheck.Model}]",
                ShowClear = true,
                Highlight = true,
                Placeholder = $"@[{elem.RefTableCheck.Model}.All]",
                Url = elem.RefTableCheck.Path,
                Bindings = b => b.SetBinding(nameof(SelectorSimple.Value), new Bind($"Parent.Filter.{filter.Column}")),
            }
        };
    }

    internal Taskpad? IndexTaskpad(FormTaskPad? taskPad)
    {
        if (taskPad == null || taskPad.Filters.Count == 0)
            return null;
        return new Taskpad()
        {
            Children = [
                new Panel() {
                    Header = "@[Filters]",
                    Collapsible = true,
                    Style = PaneStyle.Transparent,
                    Children = [..taskPad.Filters.Select(CreateFilterControl)]
                },
            ]
        };
    }
    internal Dialog CreateBrowseDialogXaml(FormMetadata dialog)
    {
        var selectCommand = new BindCmd() { Command = CommandType.Select };
        selectCommand.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("Parent.ItemsSource"));
        return new Dialog()
        {
            CollectionView = XamlCollectionView(),
            Width = Length.FromString("60rem"), // TODO
            Height = Length.FromString("40rem"),
            Title = $"@[{Table.Model}.Browse]",
            Buttons = [
                new Button()
                {
                    Style = ButtonStyle.Primary,
                    Content = "@[Select]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), selectCommand)
                },
                new Button()
                {
                    Content = "@[Cancel]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd() {Command = CommandType.Close })
                },
            ],
            Children = [
                new Grid(_xamlServiceProvider)
                {
                    Rows = RowDefinitions.FromString("Auto,1*,Auto"),
                    Height = Length.FromString("100%"),
                    Children = [
                        ..dialog.Elements.Select(ElementToControl),
                        new Pager()
                        {
                            Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))
                        }
                    ]
                }
            ],
            Taskpad = IndexTaskpad(dialog.TaskPad)
        };
    }
}

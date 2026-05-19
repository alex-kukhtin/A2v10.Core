// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    IEnumerable<DataGridColumn> IndexColumnsXaml(Boolean hasChecked) =>
        Table.IndexForm().Columns.Select(col =>
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
        foreach (var f in Table.IndexForm().Filters)
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
    Pager XamlPager() =>
        new()
        {
            Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))
        };

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

    internal Page CreateIndexPageXaml()
    {
        return new Page()
        {
            CollectionView = XamlCollectionView(),
            Children = [IndexPageGrid()],
            Taskpad = IndexTaskpad()
        };
    }

    internal Partial CreateIndexPagePartialXaml()
    {
        var collView = XamlCollectionView();
        collView.Children = [IndexPageGrid()];
        return new Partial()
        {
            Children = [collView]
        };
    }

    internal Grid IndexPageGrid()
    {
        return new Grid(_xamlServiceProvider)
        {
            Rows = RowDefinitions.FromString("Auto,1*,Auto"),
            Height = Length.FromString("100%"),
            Children = [
                new Toolbar(_xamlServiceProvider)
                {
                    Children = [
                        ButtonCreate(),
                        ButtonEditSelected(),
                        new Separator(),
                        new Button() {
                            Icon = Icon.Reload,
                            Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(nameof(CommandType.Reload)))
                        },
                        new ToolbarAligner(),
                        new SearchBox()
                        {
                            TabIndex = 1,
                            Placeholder = "@[Search]",
                            Bindings = b => b.SetBinding(nameof(SearchBox.Value), new Bind("Parent.Filter.Fragment"))
                        }
                    ]
                },
                new DataGrid()
                {
                    FixedHeader = true,
                    Sort = true,
                    Bindings = b => {
                        b.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource"));
                    },
                    Columns = [..IndexColumnsXaml(false)]
                },
                XamlPager()
            ]
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

    internal Taskpad? IndexTaskpad()
    {
        var filters = Table.IndexForm().Filters;
        if (filters.Count == 0)
            return null;
        return new Taskpad()
        {
            Children = [
                new Panel() {
                    Header = "@[Filters]",
                    Collapsible = true,
                    Style = PaneStyle.Transparent,
                    Children = [..filters.Select(CreateFilterControl)]
                },
            ]
        };
    }
    internal Dialog CreateBrowseDialogXaml()
    {
        var selectCommand = new BindCmd() { Command = CommandType.Select };
        selectCommand.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("Parent.ItemsSource"));
        return new Dialog()
        {
            CollectionView = XamlCollectionView(),
            Width = Length.FromString("60rem"), // TODO
            Title = $"@[{Table.Model}.Browse]",
            Buttons = [
                new Button() {
                    Style = ButtonStyle.Primary,
                    Content = "@[Select]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), selectCommand)
                },
                new Button() {
                    Content = "@[Cancel]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd() {Command = CommandType.Close })
                },
            ],
            Children = [
                new Grid(_xamlServiceProvider) {
                    Children = [
                        new Toolbar(_xamlServiceProvider),
                        new DataGrid()
                        {
                            FixedHeader = true,
                            Sort = true,
                            Height = Length.FromString("30rem"), // TODO
                            Bindings = b => {
                                b.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource"));
                                b.SetBinding(nameof(DataGrid.DoubleClick), selectCommand);
                            },
                            Columns = [..IndexColumnsXaml(false)],
                        },
                        XamlPager()
                    ]
                }
            ],
            Taskpad = IndexTaskpad()
        };
    }
}

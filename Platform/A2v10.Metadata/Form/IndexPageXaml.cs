// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
{
    IEnumerable<DataGridColumn> IndexColumnsXaml(Boolean hasChecked) =>
        _table.IndexForm().Columns.Select(col =>
            new DataGridColumn()
            {
                Header = col.Value.Header,
                Role = col.Value.DataType.ToXamlColumnRole(),
                SortProperty = col.Value.DataType == FormColumnType.Ref ? col.Key : null,
                Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), 
                    new Bind(col.Value.Path) { DataType = col.Value.DataType.ToXamlDataType()})
            }
         );

    IEnumerable<FilterItem> CollectionViewFilters()
    {
        yield return new FilterItem() { Property = "Fragment", DataType = DataType.String };
        foreach (var f in _table.IndexForm().Filters)
            yield return new FilterItem() { Property = f, DataType = DataType.Object };
    }

    CollectionView XamlCollectionView() =>
        new()
        {
            RunAt = RunMode.Server,
            Bindings = b => b.SetBinding(nameof(CollectionView.ItemsSource), new Bind(_table.CollectionName)),
            Filter = new FilterDescription()
            {
                Items = [..CollectionViewFilters()]
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
            if (_table.EditWith == EditWithMode.Dialog)
            {
                bindCmd.Command = CommandType.Dialog;
                bindCmd.Action = DialogAction.Append;
                bindCmd.Url = $"{_table.Path}/edit";
                bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(_table.CollectionName));
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
            if (_table.EditWith == EditWithMode.Dialog)
            {
                bindCmd.Command = CommandType.Dialog;
                bindCmd.Action = DialogAction.EditSelected;
                bindCmd.Url = $"{_table.Path}/edit";
                bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(_table.CollectionName));
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
            Children = [
                new Grid(_xamlServiceProvider) {
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
                }
            ],
            Taskpad = IndexTaskpad()
        };
    }

    UIElement CreateFilterControl(String filter)
    {
        var elem = _table.AllColumns(x => x.Name == filter).FirstOrDefault();
        if (elem == null)
            return new Block();
        return new SelectorSimple()
        {
            Label = $"@[{elem.RefTableCheck.Model}]",
            ShowClear = true,
            Highlight = true,
            Placeholder = $"@[{elem.RefTableCheck.Model}.All]",
            Url = elem.RefTableCheck.Path,
            Bindings = b => b.SetBinding(nameof(SelectorSimple.Value), new Bind($"Parent.Filter.{filter}")),
        };
    }

    internal Taskpad? IndexTaskpad()
    {
        var filters = _table.IndexForm().Filters;
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

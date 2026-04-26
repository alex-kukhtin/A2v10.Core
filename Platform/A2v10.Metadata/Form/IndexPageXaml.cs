// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using A2v10.Xaml;
using System.Globalization;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
{
    IEnumerable<DataGridColumn> IndexColumnsXaml(Boolean hasChecked)
    {
        yield return new DataGridColumn()
        {
            Role = ColumnRole.Id,
            Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), new Bind("Id"))
        };
        yield return new DataGridColumn()
        {
            LineClamp = 2,
            Header = "@[Name]",
            Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), new Bind("Name"))
        };
        yield return new DataGridColumn()
        {
            LineClamp = 2,
            Header = "@[Memo]",
            Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), new Bind("Memo"))
        };
    }

    CollectionView XamlCollectionView() =>
        new()
        {
            RunAt = RunMode.Server,
            Bindings = b => b.SetBinding(nameof(CollectionView.ItemsSource), new Bind(_table.CollectionName)),
            Filter = new FilterDescription()
            {
                Items = [
                    new FilterItem() {Property = "Fragment", DataType = DataType.String }
                ]
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
                bindCmd.Url = $"/{_table.Path}/edit";
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
                bindCmd.Url = $"/{_table.Path}/edit";
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
                                    Bindings = b => b.SetBinding(nameof(SearchBox.Value), new Bind("Parent.Filter.Fragment"))
                                }
                            ]
                        },
                        new DataGrid()
                        {
                            FixedHeader = true,
                            Sort = true,
                            Bindings = b => b.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource")),
                            Columns = [..IndexColumnsXaml(false)]
                        },
                        XamlPager()
                    ]
                }
            ]
        };
    }

    internal Dialog CreateBrowseDialogXaml()
    {
        return new Dialog()
        {
            CollectionView = XamlCollectionView(),
            Children = [
                new Grid(_xamlServiceProvider) {
                    Children = [
                        new Toolbar(_xamlServiceProvider),
                        new DataGrid()
                        {
                            FixedHeader = true,
                            Sort = true,
                            Bindings = b => b.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource")),
                            Columns = [..IndexColumnsXaml(false)]
                        },
                        XamlPager()
                    ]
                }
            ]
        };
    }
}

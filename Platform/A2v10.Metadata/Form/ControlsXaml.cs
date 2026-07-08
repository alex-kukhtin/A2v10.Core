// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;

using XTable = A2v10.Xaml.Table;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{

    UIElementBase ElementToTableCell(FormColumn elem)
    {
        return elem.DataType switch
        {
            FormColumnType.String => new TableCell()
                {
                    Bindings = b => b.SetBinding(nameof(TableCell.Content), new Bind(elem.TableColumn.Name))
                },
            _ => new TableCell() { Content = elem }
        };
    }

    Table CreateDetailsTable(FormTab tab)
    {
        return new Table()
        {
            GridLines = GridLinesVisibility.Both,
            Bindings = b => b.SetBinding(nameof(XTable.ItemsSource), new Bind($"{Table.Model}.{tab.Scope}")),
            Header = [
                new TableRow()
                {

                }
            ],
            Rows = [
                new TableRow()
                {
                    Cells = [..tab.Columns.Select(c => ElementToTableCell(c.Value))]
                }
            ]
        };
    }

    UIElementBase CreateTabsScope(FormTabs tabs)
    {
        return new Switch()
        {
            Bindings = b => b.SetBinding(nameof(Switch.Expression), new Bind($"{Table.Model}.$$Tab")),
            Cases = [..tabs.Tabs.Select(tab =>
                new Case()
                {
                    Value = tab.Scope,
                    Children = [
                        new Grid(_xamlServiceProvider)
                        {
                            Rows = RowDefinitions.FromString("Auto,1*"),
                            Children = [
                                new Toolbar(_xamlServiceProvider)
                                {
                                    Children = [
                                        new Button() {
                                            Content = "@[AddRow]",
                                            Icon = Icon.Plus
                                        }
                                    ]
                                },
                                CreateDetailsTable(tab)
                            ]
                        }
                    ]
                })
            ]
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
                Children = [.. fg.Columns.Select(x => CreateEditControl(x.Value))]
            },
            FormTabs ft => new Grid(_xamlServiceProvider)
            {
                Rows = RowDefinitions.FromString("Auto,1*"),
                Children = [
                    new TabBar()
                    {
                        Bindings = b => b.SetBinding(nameof(TabBar.Value), new Bind($"{Table.Model}.$$Tab")),
                        Buttons = [..ft.Tabs.Select(tab => new TabButton() { Content = $"@[{tab.Scope}]", ActiveValue=tab.Scope })]
                    },
                    CreateTabsScope(ft)
               ]
            },

            _ => throw new InvalidOperationException($"Invalid control {elem}")
        };
    }
}

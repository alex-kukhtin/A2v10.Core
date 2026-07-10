// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;
using XTable = A2v10.Xaml.Table;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    UIElementBase ElementToTableCell(TableColumn elem)
    {
        return elem.Type switch
        {
            ColumnType.RowNumber => new TableCell()
                {
                    Align = TextAlign.Right,
                    Bindings = b => b.SetBinding(nameof(TableCell.Content), new Bind(elem.Name) { DataType = DataType.Number })
                },
            ColumnType.Ref => new SelectorSimple()
                {
                    Url = elem.RefTableCheck.Path,
                    Bindings = b => b.SetBinding(nameof(SelectorSimple.Value), new Bind(elem.Name))
                },
            _ => new TextBox()
                {
                    Align = elem.Type.ToXamlAlign(),
                    Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind(elem.Name) { DataType = elem.Type.ToXamlDataType() })
                },
        };
    }

    XTable CreateDetailsTable(FormElement tab)
    {
        return new Table()
        {
            GridLines = GridLinesVisibility.Both,
            Bindings = b => b.SetBinding(nameof(XTable.ItemsSource), new Bind($"{Table.Model}.{tab.Scope}")),
            Header = [
                new TableRow()
                {
                    Cells = [..tab.Columns.Select(c => 
                        new TableCell() { 
                            Content = c.Header 
                        })
                    ]
                }
            ],
            Rows = [
                new TableRow()
                {
                    Cells = [..tab.Columns.Select(c => ElementToTableCell(c))]
                }
            ]
        };
    }

    Button AddRowButton(FormElement tab)
    {
        var addRowCommand = new BindCmd() { Command = CommandType.Append };
        addRowCommand.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind($"{Table.Model}.{tab.Scope}"));
        return new Button()
        {
            Content = "@[AddRow]",
            Icon = Icon.Plus,
            Bindings = b => b.SetBinding(nameof(Button.Command), addRowCommand)
        };
    }

    Switch CreateTabsScope(FormElement tabs)
    {
        return new Switch()
        {
            Bindings = b => b.SetBinding(nameof(Switch.Expression), new Bind($"{Table.Model}.$$Tab")),
            Cases = [..tabs.Elements.Select(tab =>
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
                                    Children = [AddRowButton(tab)]
                                },
                                CreateDetailsTable(tab)
                            ]
                        }
                    ]
                })
            ]
        };
    }

    UIElementBase CreateFilterControl(TableColumn column)
    {
        return column.Type switch
        {
            ColumnType.Date or ColumnType.DateTime =>
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
            ColumnType.Ref => new SelectorSimple()
            {
                Label = $"@[{column.RefTableCheck.Model}]",
                ShowClear = true,
                Highlight = true,
                Placeholder = $"@[{column.RefTableCheck.Model}.All]",
                Url = column.RefTableCheck.Path,
                Bindings = b => b.SetBinding(nameof(SelectorSimple.Value), new Bind($"Parent.Filter.{column.Name}")),
            },
            _ =>
                throw new InvalidOperationException($"Invalid filter control type '{column.Type}'")
        };
    }

    UIElementBase ElementToControl(FormElement elem)
    {
        return elem.Is switch
        {
            FormElementKind.Toolbar => new Toolbar(_xamlServiceProvider)
            {
                Children = [.. elem.Commands.Select(ToolbarControl)]
            },
            FormElementKind.DataGrid => new DataGrid()
            {
                FixedHeader = true,
                Sort = true,
                Bindings = b =>
                {
                    b.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource"));
                },
                Columns = [.. IndexColumnsXaml(elem.Columns, false)]
            },
            FormElementKind.Pager => new Pager()
            {
                Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))
            },
            FormElementKind.Group => new Grid(_xamlServiceProvider)
            {
                Children = [.. elem.Columns.Select(CreateEditControl)]
            },
            FormElementKind.Tabs => new Grid(_xamlServiceProvider)
            {
                Rows = RowDefinitions.FromString("Auto,1*"),
                Children = [
                    new TabBar()
                    {
                        Bindings = b => b.SetBinding(nameof(TabBar.Value), new Bind($"{Table.Model}.$$Tab")),
                        Buttons = [..elem.Elements.Select(tab => new TabButton() { Content = $"@[{tab.Scope}]", ActiveValue=tab.Scope })]
                    },
                    CreateTabsScope(elem)
               ]
            },
            FormElementKind.Filters => new Panel()
            {
                Collapsible = true,
                Header = "@[Filters]",
                Style = PaneStyle.Transparent,
                Children = [.. elem.Columns.Select(CreateFilterControl)]
            },
            FormElementKind.Taskpad => new Taskpad()
            {
                Children = [.. elem.Elements.Select(ElementToControl)]
            },
            _ => throw new InvalidOperationException($"Invalid control {elem.Is}")
        };
    }
}

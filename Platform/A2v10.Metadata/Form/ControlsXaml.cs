// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;
using XTable = A2v10.Xaml.Table;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    static UIElementBase ElementToTableCell(TableColumn elem)
    {
        return elem.Type switch
        {
            ColumnType.RowNumber => new TableCell()
                {
                    Align = TextAlign.Right,
                    CssClass = elem.Type.ToXamlSemanticClass(),
                    Bindings = b => b.SetBinding(nameof(TableCell.Content), new Bind(elem.Name) { DataType = DataType.Number })
                },
            ColumnType.Ref => new SelectorSimple()
                {
                    Url = elem.RefTableCheck.Path,
                    CssClass = elem.Type.ToXamlSemanticClass(),
                    Bindings = b => b.SetBinding(nameof(SelectorSimple.Value), new Bind(elem.Name))
                },
            _ => new TextBox()
                {
                    Align = elem.Type.ToXamlAlign(),
                    CssClass = elem.Type.ToXamlSemanticClass(),
                    Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind(elem.Name) { DataType = elem.Type.ToXamlDataType() })
                },
        };
    }

    XTable CreateDetailsTable(FormElement tab)
    {
        TableCell RemoveRowCell()
        {
            var removeCmd = new BindCmd()
            {
                Command = CommandType.Remove,
                Confirm = new Confirm() { Message = "@[Confirm.Delete.Row]" }

            };
            removeCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind());
            return new TableCell()
            {
                Align = TextAlign.Center,
                Content = new Hyperlink()
                {
                    Content = "✕",
                    Bindings = b => b.SetBinding(nameof(Hyperlink.Command), removeCmd)
                }
            };
        }

        return new Table()
        {
            GridLines = GridLinesVisibility.Both,
            StickyHeaders = true,
            Height = Length.FromString("100%"),
            Bindings = b => b.SetBinding(nameof(XTable.ItemsSource), new Bind($"{Table.Model}.{tab.Scope}")),
            Header = [
                new TableRow()
                {
                    Cells = [..tab.Columns.Select(c =>
                        new TableCell() {
                            Content = c.Header
                        }),
                        new TableCell()
                    ]
                }
            ],
            Rows = [
                new TableRow()
                {
                    Cells = [..tab.Columns.Select(c => ElementToTableCell(c)), RemoveRowCell()]
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
                            MinHeight = Length.FromString("0"),
                            Height = Length.FromString("100%"),
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
            ColumnType.Ref or ColumnType.Operation => new SelectorSimple()
            {
                Label = $"@[{column.RefTableCheck.Storage.Model}]",
                ShowClear = true,
                Highlight = true,
                Placeholder = $"@[{column.RefTableCheck.Storage.Model}.All]",
                Url = column.RefTableCheck.Path,
                Bindings = b => b.SetBinding(nameof(SelectorSimple.Value), new Bind($"Parent.Filter.{column.Name}")),
            },
            ColumnType.Document => new SelectorSimple()
            {
                Label = $"@[{column.RefTableCheck.Storage.Model}]",
                ShowClear = true,
                Highlight = true,
                Placeholder = $"@[{column.RefTableCheck.Storage.Model}.All]",
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
            FormElementKind.Group => new FlowPanel(_xamlServiceProvider)
            {
                Axis = elem.Axis == FlowAxis.Columns ? Xaml.FlowAxis.Columns : Xaml.FlowAxis.Rows,
                LabelAt = elem.LabelAt == LabelAt.Top ? FlowLabelAt.Top : FlowLabelAt.Left,
                Children = [.. elem.Columns.Select(CreateEditControl)]
            },
            FormElementKind.Tabs => new Grid(_xamlServiceProvider)
            {
                Rows = RowDefinitions.FromString("Auto,1*"),
                MinHeight = Length.FromString("0"),
                Height = Length.FromString("100%"),
                Children = [
                    new TabBar()
                    {
                        Bindings = b => b.SetBinding(nameof(TabBar.Value), new Bind($"{Table.Model}.$$Tab")),
                        Buttons = [.. elem.Elements.Select<FormElement, TabButton>(tab =>
                            new TabButton() {
                                Content = $"@[{tab.Scope}]",
                                ActiveValue= tab.Scope,
                                Bindings = b => b.SetBinding(nameof(TabButton.Badge), new Bind($"{Table.Model}.{tab.Scope}.Count"))
                            })
                        ]
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
    UIElementBase CreateEditControl(TableColumn column)
    {
        var valueBind = new Bind($"{Table.Model}.{column.Name}")
        {
            DataType = column.Type.ToXamlDataType(),
        };
        return column.Type switch
        {
            ColumnType.Date => new DatePicker()
            {
                Label = column.Header,
                CssClass = column.Type.ToXamlSemanticClass(),
                Bindings = b => b.SetBinding(nameof(DatePicker.Value), valueBind)
            },
            ColumnType.Name => new TextBox()
            {
                Label = column.Header,
                Bold = true,
                TabIndex = 1,
                CssClass = column.Type.ToXamlSemanticClass(),
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            },
            ColumnType.Memo => new TextBox()
            {
                Label = column.Header,
                Multiline = true,
                Rows = 3,
                CssClass = column.Type.ToXamlSemanticClass(),
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            },
            ColumnType.Operation => new Header()
            {
                Bindings = b => b.SetBinding(nameof(Header.Content), new Bind($"{Table.Model}.{column.Name}.Name"))
            },
            ColumnType.Ref or ColumnType.Document => new SelectorSimple()
            {
                Label = column.Header,
                CssClass = column.Type.ToXamlSemanticClass(),
                Url = column.RefTableCheck.Path,
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            },
            ColumnType.Done or ColumnType.Bit or ColumnType.Boolean => new CheckBox()
            {
                Label = column.Header,
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            },
            ColumnType.Money or ColumnType.Float or 
            ColumnType.Decimal => new TextBox()
            {
                Label = column.Header,
                Align = TextAlign.Right,
                CssClass = column.Type.ToXamlSemanticClass(),
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            },
            _ => new TextBox()
            {
                Label = column.Header,
                CssClass = column.Type.ToXamlSemanticClass(),
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            }
        };
    }

}

// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.Xaml;
using XTable = A2v10.Xaml.Table;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    /* What this part of the form inherits, by the reference that drives it. The root is the
     * endpoint's own; a scoped element asks the collection it is anchored to, for its kind.
     */
    private Dictionary<String, InheritDescriptor[]> InheritsOf(FormElement elem) =>
        String.IsNullOrEmpty(elem.Scope)
            ? Endpoint.Declaration.Inherits
            : Endpoint.Declaration.Details[elem.Scope].RowSets
                .First(rs => rs.Kind == elem.Kind).Inherits;

    /* A selector picks by typing as well as from the dialog, and the two have to hand the row
     * the same object. The dialog returns the element whole; fetch returns Id and Name, so what
     * else this reference feeds has to be named to it - and it is named here, because the
     * catalog on the other end has no way to know who is picking from it.
     *
     * The names are the SOURCE columns - what the catalog is asked for - not the fields they
     * land in here. Both spellings exist because they are different tables.
     */
    private static String SelectorUrl(Dictionary<String, InheritDescriptor[]> inherits, TableColumn column)
    {
        var path = column.RefTableCheck.Path;
        return inherits.TryGetValue(column.Name, out var inh) && inh.Length > 0
            ? $"{path}?inherit={String.Join(',', inh.Select(x => x.Source))}"
            : path;
    }

    // rows of a collection are columns and nothing else, so the member unwraps on the way in
    static UIElementBase ElementToTableCell(MemberDescriptor member, Dictionary<String, InheritDescriptor[]> inherits)
    {
        var elem = member.ColumnCheck;
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
                    Url = SelectorUrl(inherits, elem),
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
        var inherits = InheritsOf(tab);

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
            Bindings = b => b.SetBinding(nameof(XTable.ItemsSource), new Bind($"{Table.Model}.{tab.RowSet}")),
            Header = [
                new TableRow()
                {
                    Cells = [..tab.Members.Select(m =>
                        new TableCell() {
                            Content = m.ColumnCheck.Header
                        }),
                        new TableCell()
                    ]
                }
            ],
            Rows = [
                new TableRow()
                {
                    Cells = [..tab.Members.Select(m => ElementToTableCell(m, inherits)), RemoveRowCell()]
                }
            ]
        };
    }

    Button AddRowButton(FormElement tab)
    {
        var addRowCommand = new BindCmd() { Command = CommandType.Append };
        addRowCommand.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind($"{Table.Model}.{tab.RowSet}"));
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
            Bindings = b => b.SetBinding(nameof(Switch.Expression), new Bind($"{Table.Model}.{tabs.TabState}")),
            Cases = [..tabs.Elements.Select(tab =>
                new Case()
                {
                    Value = tab.RowSet,
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

    // one control per filter, by KIND: the panel can no longer be handed a column it cannot draw
    UIElementBase CreateFilterControl(FilterDescriptor filter)
    {
        return filter.Kind switch
        {
            FilterKind.Period => new PeriodPicker()
            {
                Label = $"@[{filter.Name}]",
                Placement = DropDownPlacement.BottomRight,
                Display = DisplayMode.Name,
                Bindings = b =>
                {
                    b.SetBinding(nameof(PeriodPicker.Value), new Bind($"Parent.Filter.{filter.Name}"));
                    b.SetBinding(nameof(PeriodPicker.Description), new Bind($"Parent.Filter.{filter.Name}.Name"));
                }
            },
            FilterKind.Ref => new SelectorSimple()
            {
                Label = $"@[{filter.ColumnCheck.RefTableCheck.Storage.Model}]",
                ShowClear = true,
                Highlight = true,
                Placeholder = $"@[{filter.ColumnCheck.RefTableCheck.Storage.Model}.All]",
                Url = filter.ColumnCheck.RefTableCheck.Path,
                Bindings = b => b.SetBinding(nameof(SelectorSimple.Value), new Bind($"Parent.Filter.{filter.Name}")),
            },
            // candidates are rows, not a shape: ItemsSource is the root 'Tags' recordset, unprefixed
            FilterKind.Tags => new TagsFilter()
            {
                Label = $"@[{filter.Name}]",
                Placeholder = "@[Placeholder.AllTags]",
                Bindings = b =>
                {
                    b.SetBinding(nameof(TagsFilter.Value), new Bind($"Parent.Filter.{filter.Name}"));
                    b.SetBinding(nameof(TagsFilter.ItemsSource), new Bind(filter.Name));
                }
            },
            _ =>
                throw new InvalidOperationException($"Invalid filter kind '{filter.Kind}'")
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
                Columns = [.. IndexColumnsXaml(Table, elem.Members)]
            },
            FormElementKind.Pager => new Pager()
            {
                Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))
            },
            FormElementKind.Group => CreateGroupPanel(elem),
            FormElementKind.Tabs => new Grid(_xamlServiceProvider)
            {
                Rows = RowDefinitions.FromString("Auto,1*"),
                MinHeight = Length.FromString("0"),
                Height = Length.FromString("100%"),
                Children = [
                    new TabBar()
                    {
                        Bindings = b => b.SetBinding(nameof(TabBar.Value), new Bind($"{Table.Model}.{elem.TabState}")),
                        Buttons = [.. elem.Elements.Select<FormElement, TabButton>(tab =>
                            new TabButton() {
                                // caption keys off the part the user sees named - the kind,
                                // or the collection when there are none
                                Content = $"@[{tab.Kind ?? tab.Scope}]",
                                ActiveValue= tab.RowSet,
                                Bindings = b => b.SetBinding(nameof(TabButton.Badge), new Bind($"{Table.Model}.{tab.RowSet}.Count"))
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
                Children = [.. elem.BakedFilters.Select(CreateFilterControl)]
            },
            FormElementKind.Taskpad => new Taskpad()
            {
                Children = [.. elem.Elements.Select(ElementToControl)]
            },
            _ => throw new InvalidOperationException($"Invalid control {elem.Is}")
        };
    }
    FlowPanel CreateGroupPanel(FormElement elem)
    {
        var inherits = InheritsOf(elem);
        return new FlowPanel(_xamlServiceProvider)
        {
            Axis = elem.Axis == FlowAxis.Columns ? Xaml.FlowAxis.Columns : Xaml.FlowAxis.Rows,
            LabelAt = elem.LabelAt == LabelAt.Top ? FlowLabelAt.Top : FlowLabelAt.Left,
            Children = [.. elem.Members.Select(m => CreateMemberControl(m, inherits))]
        };
    }

    // the only place a member that is not a column reaches a control - see CLAUDE.md, "Members"
    UIElementBase CreateMemberControl(MemberDescriptor member,
        Dictionary<String, InheritDescriptor[]> inherits) => member.Kind switch
    {
        MemberKind.Column => CreateEditControl(member.ColumnCheck, inherits),
        // value is the record's own tags, candidates the root 'Tags' array - the pair Load emits
        MemberKind.Tags => new TagsControl()
        {
            Label = $"@[{member.Name}]",
            Placeholder = "@[Tag.Choose]",
            Bindings = b =>
            {
                b.SetBinding(nameof(TagsControl.Value), new Bind($"{Table.Model}.{member.Name}"));
                b.SetBinding(nameof(TagsControl.ItemsSource), new Bind(member.Name));
                var cmd = new BindCmd()
                {
                    Command = CommandType.Dialog,
                    Action = DialogAction.Show,
                    Url = TagEndpointMetadata.SettingsUrl(Table.Model)
                };
                b.SetBinding(nameof(TagsControl.SettingsCommand), cmd);
            }
        },
        _ => throw new InvalidOperationException($"Invalid member kind '{member.Kind}'")
    };

    UIElementBase CreateEditControl(TableColumn column, Dictionary<String, InheritDescriptor[]> inherits)
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
                Url = SelectorUrl(inherits, column),
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

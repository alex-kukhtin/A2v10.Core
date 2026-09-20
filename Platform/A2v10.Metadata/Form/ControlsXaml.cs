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

    /* The picker for a state, in the three places one appears - the card, a row of a collection,
     * the filter panel. Everything but the reach is the same, and the one difference that matters
     * is the ITEM's value: in the card and in a row the property holds the element resolved through
     * the map, so the item binds to the element itself; in the filter it holds the bare code, which
     * is what the WHERE compares (see FilterKind.Set).
     *
     * A ColorComboBox and not a ComboBox with a colour: the list is what a state IS, and the grid
     * draws the same badge. The control is old - it is what the tags dialog picks a colour with -
     * so the kind added a vocabulary entry, not a control.
     */
    static ColorComboBox StatePicker(String itemsSource, Bind value, Bind itemValue,
        String? label = null, String? cssClass = null) => new()
    {
        Label = label,
        CssClass = cssClass,
        Children = [
            new ColorComboBoxItem()
            {
                Bindings = b =>
                {
                    b.SetBinding(nameof(ColorComboBoxItem.Content), new Bind(Constants.FieldNames.Name));
                    b.SetBinding(nameof(ColorComboBoxItem.Value), itemValue);
                    b.SetBinding(nameof(ColorComboBoxItem.Color), new Bind(Constants.FieldNames.Color));
                }
            }
        ],
        Bindings = b =>
        {
            b.SetBinding(nameof(ColorComboBox.ItemsSource), new Bind(itemsSource));
            b.SetBinding(nameof(ColorComboBox.Value), value);
        }
    };

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
            ColumnType.Ref or ColumnType.Account => new SelectorSimple()
                {
                    Url = SelectorUrl(inherits, elem),
                    CssClass = elem.Type.ToXamlSemanticClass(),
                    Bindings = b => b.SetBinding(nameof(SelectorSimple.Value), new Bind(elem.Name))
                },
            // the row's own picker; 'Root.' is the reach - the candidates are an array at the model
            // root, not a property of the row ($data, RenderContext.GetNormalizedPath)
            ColumnType.State => StatePicker(
                $"Root.{elem.RefTableCheck.Storage.CollectionName}",
                new Bind(elem.Name), new Bind(), cssClass: elem.Type.ToXamlSemanticClass()),
            /* The same control as in the card, and the same reason - see CreateEditControl. The one
             * difference is the reach: here the scope is the ROW, and the candidates are an array at
             * the model root, which is what 'Root.' says ($data, RenderContext.GetNormalizedPath).
             */
            ColumnType.Enum => new ComboBox()
                {
                    CssClass = elem.Type.ToXamlSemanticClass(),
                    Children = [
                        new ComboBoxItem()
                        {
                            Bindings = b =>
                            {
                                b.SetBinding(nameof(ComboBoxItem.Content), new Bind(Constants.FieldNames.Name));
                                b.SetBinding(nameof(ComboBoxItem.Value), new Bind());
                            }
                        }
                    ],
                    Bindings = b =>
                    {
                        b.SetBinding(nameof(ComboBox.ItemsSource),
                            new Bind($"Root.{elem.RefTableCheck.Storage.CollectionName}"));
                        b.SetBinding(nameof(ComboBox.Value), new Bind(elem.Name));
                    }
                },
            /* The card's control, and a cell is where it was missing: a date fell through to the
             * default and was edited as text, typed by DataType alone. No width - in a cell the
             * column decides, which is why the card's 12rem does not travel here.
             */
            ColumnType.Date => new DatePicker()
            {
                CssClass = elem.Type.ToXamlSemanticClass(),
                Bindings = b => b.SetBinding(nameof(DatePicker.Value), new Bind(elem.Name) { DataType = elem.Type.ToXamlDataType() })
            },
            // the card's control, compact because it stands in a cell - the tags dialog's own shape
            ColumnType.Color => new ColorPicker()
            {
                Compact = true,
                CssClass = elem.Type.ToXamlSemanticClass(),
                Bindings = b => b.SetBinding(nameof(ColorPicker.Value), new Bind(elem.Name))
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
            /* The whole set arrives with the page, so the candidates are an array in the model and
             * not an address to browse - which is the entire difference from the branch above.
             *
             * What lands in the Filter is the CODE and not an object: it is what the column stores
             * and what the WHERE compares, and it lets the set's own 'All' row (Id = N'') be a
             * value like any other instead of a state the control would have to invent.
             */
            /* The same badge the grid draws, so the panel and the rows read as one thing. The item's
             * value is the CODE here and the element everywhere else - the line above says why.
             */
            FilterKind.Set when filter.ColumnCheck.Type == ColumnType.State => StatePicker(
                filter.ColumnCheck.RefTableCheck.Storage.CollectionName,
                new Bind($"Parent.Filter.{filter.Name}"), new Bind(Constants.FieldNames.Id),
                label: $"@[{filter.ColumnCheck.RefTableCheck.Storage.Model}]"),
            FilterKind.Set => new ComboBox()
            {
                Label = $"@[{filter.ColumnCheck.RefTableCheck.Storage.Model}]",
                Highlight = true,
                Children = [
                    new ComboBoxItem()
                    {
                        Bindings = b =>
                        {
                            b.SetBinding(nameof(ComboBoxItem.Content), new Bind(Constants.FieldNames.Name));
                            b.SetBinding(nameof(ComboBoxItem.Value), new Bind(Constants.FieldNames.Id));
                        }
                    }
                ],
                Bindings = b =>
                {
                    b.SetBinding(nameof(ComboBox.ItemsSource),
                        new Bind(filter.ColumnCheck.RefTableCheck.Storage.CollectionName));
                    b.SetBinding(nameof(ComboBox.Value), new Bind($"Parent.Filter.{filter.Name}"));
                }
            },
            /* The one filter whose candidates are not data: the roles are a closed set of the
             * platform, so the items are written out here and no recordset carries them. 'All' is
             * written out with them for the same reason - a set gets that row from the deploy,
             * this list has no deploy to get it from.
             *
             * The keys name the set, as a value's do ('@[{Model}.{Id}]'): two enums with a member
             * called 'Initial' must not collapse into one translation.
             */
            FilterKind.Role => new ComboBox()
            {
                Label = $"@[{nameof(StateRole)}]",
                Highlight = true,
                Children = [
                    // 'All' is written out with them: a set is given that row by the deploy, and
                    // this list has no deploy to be given anything by
                    new ComboBoxItem() { Content = $"@[{nameof(StateRole)}.All]", Value = String.Empty },
                    .. Enum.GetNames<StateRole>().Select(n => new ComboBoxItem()
                    {
                        Content = $"@[{nameof(StateRole)}.{n}]",
                        Value = n
                    })
                ],
                Bindings = b => b.SetBinding(nameof(ComboBox.Value), new Bind($"Parent.Filter.{filter.Name}"))
            },
            // candidates are rows, not a shape: ItemsSource is the root 'Tags' recordset, unprefixed
            FilterKind.Tags => new TagsFilter()
            {
                Label = $"@[{filter.Name}]",
                Placeholder = "@[Filter.Tag.All]",
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
                Children = [.. elem.Commands.Select(c => ToolbarControl(c, CommandScope.Grid))]
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
            // the whole tree is in the model: bound to the collection itself, not to a collection view
            FormElementKind.TreeGrid => new TreeGrid()
            {
                Hover = true,
                StickyHeaders = true,
                Height = Length.FromString("100%"),
                GridLines = GridLinesVisibility.Horizontal,
                ItemsProperty = Constants.FieldNames.Items,
                Bindings = b => b.SetBinding(nameof(TreeGrid.ItemsSource), new Bind(Table.CollectionName)),
                Columns = [.. elem.Members.Select(m => m.ColumnCheck).Select((col, ix) => new TreeGridColumn()
                {
                    Header = col.Header,
                    // the first column carries the expand toggle
                    ShowButton = ix == 0,
                    Fit = col.IsKey,
                    Wrap = col.IsKey ? WrapMode.NoWrap : WrapMode.Default,
                    Bindings = b => b.SetBinding(nameof(TreeGridColumn.Content),
                        new Bind(col.DisplayPath) { DataType = col.Type.ToXamlDataType() })
                })]
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
    /* A group lays out members and groups alike: 'fields' are the editors it carries itself,
     * 'elements' the groups under it - a row of two columns is one of these inside another, and
     * that is the ordinary way to lay out a card. Fields come first because the file is read top to
     * bottom and they are written first; two keys cannot interleave, so the order is decided once
     * here rather than guessed per form.
     *
     * A nested group is never scoped - CheckElement refuses a scope anywhere but on a tab - so what
     * it inherits is the record's own, the same thing the group above it reads.
     */
    FlowPanel CreateGroupPanel(FormElement elem)
    {
        var inherits = InheritsOf(elem);
        return new FlowPanel(_xamlServiceProvider)
        {
            Axis = elem.Axis == FlowAxis.Columns ? Xaml.FlowAxis.Columns : Xaml.FlowAxis.Rows,
            LabelAt = elem.LabelAt == LabelAt.Top ? FlowLabelAt.Top : FlowLabelAt.Left,
            Children = [
                .. elem.Members.Select(m => CreateMemberControl(m, inherits)),
                .. elem.Elements.Select(ElementToControl)
            ]
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
        var valueBind = new Bind($"{Table.Model}.{column.ModelName}")
        {
            DataType = column.Type.ToXamlDataType(),
        };
        /* A closed set of the platform, stored by name: the items are the enum's members, the text
         * their localization keys - the same keys the tree sends.
         */
        ComboBox ClosedSet<T>() where T : struct, Enum => new()
        {
            Label = column.Header,
            Children = [.. Enum.GetNames<T>().Select(n => new ComboBoxItem()
            {
                Content = $"@[{column.Name}.{n}]",
                Value = n
            })],
            Bindings = b => b.SetBinding(nameof(ComboBox.Value), valueBind)
        };

        // the chart's own columns; an author field of the same name elsewhere is an ordinary string
        if (Table.Kind == EndpointKind.AccPlan && column.Name == Constants.FieldNames.AccountType)
            return ClosedSet<AccountType>();
        if (Table.Kind == EndpointKind.AccPlan && column.Name == Constants.FieldNames.NormalBalance)
            return ClosedSet<NormalBalance>();

        return column.Type switch
        {
            /* A width, for the same reason the colour has one: a date is a control of a known size,
             * and in a row everything that names no width takes the leftover. It holds in a column
             * too - a date box across the whole card was never what it is.
             */
            ColumnType.Date => new DatePicker()
            {
                Label = column.Header,
                Width = Length.FromString("12rem"),
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
            /* The control the tags dialog has picked a colour with all along, so the vocabulary and
             * what draws it are the same on both roads. Until now the column fell through to the
             * default and a colour was typed as a string - the one value where a typo is invisible
             * on the card and shows as an unpainted badge somewhere else.
             */
            ColumnType.Color => new ColorPicker()
            {
                Label = column.Header,
                Width = Length.FromString("8rem"),
                CssClass = column.Type.ToXamlSemanticClass(),
                Bindings = b => b.SetBinding(nameof(ColorPicker.Value), valueBind)
            },
            ColumnType.Ref or ColumnType.Document or ColumnType.Account => new SelectorSimple()
            {
                Label = column.Header,
                CssClass = column.Type.ToXamlSemanticClass(),
                Url = SelectorUrl(inherits, column),
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            },
            /* The set arrives with the record, so there is nothing to browse - the same reason the
             * filter is a ComboBox. The item's Value binds to the ELEMENT and not to its Id: what
             * the property holds stays an object, resolved through the map like every other
             * reference, so the save reads its Id the way it does for all of them. The filter is
             * the opposite case and for its own reason - there the value IS the code.
             */
            // the whole list rides with the record, as an enum's does; what it adds is the colour
            ColumnType.State => StatePicker(
                column.RefTableCheck.Storage.CollectionName, valueBind, new Bind(),
                label: column.Header, cssClass: column.Type.ToXamlSemanticClass()),
            ColumnType.Enum => new ComboBox()
            {
                Label = column.Header,
                CssClass = column.Type.ToXamlSemanticClass(),
                Children = [
                    new ComboBoxItem()
                    {
                        Bindings = b =>
                        {
                            b.SetBinding(nameof(ComboBoxItem.Content), new Bind(Constants.FieldNames.Name));
                            b.SetBinding(nameof(ComboBoxItem.Value), new Bind());
                        }
                    }
                ],
                Bindings = b =>
                {
                    b.SetBinding(nameof(ComboBox.ItemsSource),
                        new Bind(column.RefTableCheck.Storage.CollectionName));
                    b.SetBinding(nameof(ComboBox.Value), valueBind);
                }
            },
            /* A blank number on a new card reads as something forgotten, so the box says who will
             * fill it. A placeholder and not a value: it is what a field shows INSTEAD of one, and
             * the number arrives on save - writing anything into the binding would send that text
             * back as a number the user typed.
             *
             * Only where a numbering is declared. The same column without one is numbered by hand,
             * and '(Auto)' there is a promise nobody keeps.
             */
            ColumnType.Autonum => new TextBox()
            {
                Label = column.Header,
                CssClass = column.Type.ToXamlSemanticClass(),
                Placeholder = String.IsNullOrEmpty(Endpoint.Declaration.Autonum)
                    ? null
                    : "@[Autonum.Auto]",
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

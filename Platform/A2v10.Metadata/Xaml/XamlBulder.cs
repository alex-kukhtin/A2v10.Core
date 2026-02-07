// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.System.Xaml;
using A2v10.Xaml;

namespace A2v10.Metadata;

using AttachAction = Action<IDictionary<String, Object>, FormItem>;
internal class XamlBulder(EditWithMode _editWith)
{
    private readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    public static String GetXaml(Object elem)
    {
        if (elem is not IRootContainer)
            throw new InvalidOperationException($"XamlBuilder.GetXaml. Invalid element type ({elem.GetType().Name}). Expected IRootContainer");
        if (elem is IInitComplete initComplete)
            initComplete.InitComplete();
        var xamlWriter = new XamlWriter();
        return xamlWriter.GetXaml(elem);
    }

    public static RootContainer BuildForm(Form form)
    {
        var b = new XamlBulder(form.EditWith);
        return form.Is switch
        {
            FormItemIs.Page => b.BuildPage(form),
            FormItemIs.Dialog => b.BuildDialog(form),
            _ => throw new InvalidOperationException($"Build {form.Is}")
        };
    }

    private static CollectionView? CreateCollectionView(Form form)
    {
        if (!form.UseCollectionView)
            return null;

        IEnumerable<FilterItem> Filters()
        {
            yield return new FilterItem() { 
                Property = "Fragment",
                DataType = DataType.String,
            };

            var filters = form.Props?.Filters;
            if (String.IsNullOrEmpty(filters))
                yield break;
            foreach (var f in filters.Split(','))
            {
                var x = f.Split(':');
                String fName = f;
                DataType filterType = DataType.String;
                if (x.Length > 1)
                {
                    fName = x[0];
                    filterType = x[1] switch
                    {
                        "S" => DataType.String,
                        "P" => DataType.Period,
                        "O" => DataType.Object,
                        _ => throw new InvalidOperationException($"Invalid filter type: {x[1]} in filter {f}")
                    };
                }
                yield return new FilterItem() { Property = fName, DataType = filterType };
            }
        }

        return new CollectionView()
        {
            RunAt = form.Is == FormItemIs.Page ? RunMode.ServerUrl : RunMode.Server,
            Bindings = b => b.SetBinding(nameof(CollectionView.ItemsSource), new Bind(form.Data)),
            Filter = new FilterDescription()
            {
                Items = [..Filters()]
            }
        };
    }

    private UIElementBase? CreateElement(FormItem item, AttachAction? attach = null, String? param = null) 
    {
        UIElementBase? elem = item.Is switch
        {
            FormItemIs.Grid => CreateGrid(item, param),
            FormItemIs.StackPanel => CreateStackPanel(item, param), 
            FormItemIs.Panel => CreatePanel(item, param),
            FormItemIs.DataGrid => CreateDataGrid(item),
            FormItemIs.TreeView => CreateTreeView(item),
            FormItemIs.Tabs => CreateTabs(item),
            FormItemIs.Pager => CreatePager(item),
            FormItemIs.Toolbar => CreateToolbar(item),
            FormItemIs.TextBox => CreateTextBox(item),
            FormItemIs.Selector => CreateSelector(item, param),
            FormItemIs.ComboBox => CreateComboBox(item, param),
            FormItemIs.ComboBoxBit => CreateComboBoxBit(item, param),
            FormItemIs.DatePicker => CreateDatePicker(item),
            FormItemIs.PeriodPicker => CreatePeriodPicker(item, param),
            FormItemIs.CheckBox => CreateCheckBox(item),
            FormItemIs.Table => CreateTable(item),
            FormItemIs.Header => CreateHeader(item),
            FormItemIs.Label => CreateLabel(item),
            FormItemIs.Button => CreateButton(item, param),
            FormItemIs.Content => CreateSpan(item),
            FormItemIs.Static => CreateStatic(item),
            _ => throw new NotImplementedException($"Implement CreateElement: {item.Is}")
        };
        if (elem != null && attach != null)
            elem.Attach = att => attach(att, item);
        return elem;
    }
    private IEnumerable<UIElementBase> CreateElements(IEnumerable<FormItem>? items, AttachAction? attach = null, String? param = null)
    {
        if (items == null)
            yield break;
        foreach (var item in items)
        {
            UIElementBase? elem = CreateElement(item, attach, param);
            if (elem == null)
                continue;
            yield return elem;
            if (item.Is == FormItemIs.Tabs)
                yield return CreateTabsBody(item, attach);
        }
    }

    private Switch CreateTabsBody(FormItem item, AttachAction? attach = null)
    {
        Case CreateCase(FormItem ch)
        {
            return new Case()
            {
                Value = ch.Data,
                Children = [..CreateElements(ch.Items, attach, "tabsbody")]
            };
        }
        FormItem? firstItem = item.Items?.First();
        return new Switch()
        {
            Bindings = b => b.SetBinding(nameof(Switch.Expression), new Bind(item.Data)),
            Cases = [.. item.Items?.Select(CreateCase) ?? []],
            Height = Length.FromStringNull(item.Height),
            Attach = att =>
            {
                if (firstItem != null)
                    attach?.Invoke(att, firstItem);
            }
        };
    }

    private void Attach(IDictionary<String, Object> att, FormItem source)
    {
        void AttachInt32(String name, Int32 val)
        {
            if (val == 0)
                return;
            att.Add(name, val.ToString());
        }

        if (source.Grid == null)
            return;
        if (source.Grid.Row > 1)
            AttachInt32("Grid.Row", source.Grid.Row);
        if (source.Grid.Col > 1)
            AttachInt32("Grid.Col", source.Grid.Col);
        AttachInt32("Grid.RowSpan", source.Grid.RowSpan);
        AttachInt32("Grid.ColSpan", source.Grid.ColSpan);
    }

    private static TabBar CreateTabs(FormItem item)
    {
        var tabBarButtons = item.Items?.Select(c => new TabButton() { Content = c.Label.Localize(), ActiveValue = c.Data }) ?? [];

        return new TabBar()
        {
            Buttons = [..tabBarButtons],
            CssClass = item.CssClass,   
            Bindings = b => b.SetBinding(nameof(TabBar.Value), new Bind(item.Data))
        };
    }
    private Panel CreatePanel(FormItem source, String? param = null)
    {
        return new Panel()
        {
            Header = source.Label.Localize(),
            Collapsible = true,
            Style = PaneStyle.Transparent,
            Children = [.. CreateElements(source.Items, Attach, param)]
        };
    }

    private StackPanel CreateStackPanel(FormItem source, String? param)
    {
        return new StackPanel()
        {
            CssClass = source.CssClass,
            Height = Length.FromStringNull(source.Height),
            Width = Length.FromStringNull(source.Width),
            Children = [.. CreateElements(source.Items, null, param)]
        };
    }

    private Grid CreateGrid(FormItem source, String? param)
    {
        IEnumerable<RowDefinition> GridRows()
        {
            if (source.Props?.Rows == null)
                return [];
            return source.Props.Rows.Split(' ').Select(r =>
                new RowDefinition()
                {
                    Height = GridLength.FromString(r),
                }
            );
        }

        IEnumerable<ColumnDefinition> GridColumns()
        {
            if (source.Props?.Columns == null)
                return [];
            return source.Props.Columns.Split(' ').Select(r =>
                new ColumnDefinition()
                {
                    Width = GridLength.FromString(r),
                }
            );
        }

        return new Grid(_xamlServiceProvider)
        {
            Rows = [.. GridRows()],
            Columns = [..GridColumns()],
            Overflow = param == "tabsbody" ? Overflow.Hidden : null,
            CssClass = source.CssClass,
            Height = Length.FromStringNull(source.Height),
            MinHeight = Length.FromStringNull(source.MinHeight),
            Children = [..CreateElements(source.Items, Attach, param)]
        };
    }
    private static TreeView CreateTreeView(FormItem source)
    {
        // for folder
        return new TreeView()
        {
            AutoSelect = AutoSelectMode.FirstItem,
            FolderSelect = true,
            Height = Length.FromStringNull(source.Height),  
            Bindings = b =>
            {
                b.SetBinding(nameof(TreeView.ItemsSource), new Bind(source.Data));
            },
            Children = [new TreeViewItem() {
                Bindings = b =>
                {
                    b.SetBinding(nameof(TreeViewItem.Label), new Bind("Name"));
                    b.SetBinding(nameof(TreeViewItem.Icon), new Bind("Icon"));
                    b.SetBinding(nameof(TreeViewItem.ItemsSource), new Bind("SubItems"));
                    b.SetBinding(nameof(TreeViewItem.IsFolder), new Bind("HasSubItems"));
                },
            }],
            ContextMenu = new DropDownMenu()
            {
                Direction = DropDownDirection.UpRight,
                Children = [
                    new Separator(),
                ]
            }
        };
    }

    private DataGrid CreateDataGrid(FormItem source)
    {
        IEnumerable<DataGridColumn> Columns()
        {
            if (source.Items == null)
                return [];
            return source.Items.Select(c => new DataGridColumn()
            {
                Header = c.Label.Localize(),
                Role = c.ToColumnRole(),
                Editable = c.IsCheckedColumn(),
                SortProperty = c.Data.EndsWith(".Name") ? c.Data[..^5] : null,
                LineClamp = c.Props?.LineClamp ?? 0,
                Fit = c.Props?.Fit == true,
                Wrap = c.Props?.NoWrap == true ? WrapMode.NoWrap : WrapMode.Default,
                Bindings = b =>
                {
                    b.SetBinding(nameof(DataGridColumn.Content), c.TypedBind());
                    if (c.IsCheckedColumn())
                        b.SetBinding(nameof(DataGridColumn.CheckAll), new Bind(c.Data));
                }
            });
        }

        return new DataGrid()
        {
            FixedHeader = true,
            Sort = true,
            CssClass = source.CssClass,
            Columns = [.. Columns()],
            Height = Length.FromStringNull(source.Height),
            Bindings = b =>
            {
                b.SetBinding(nameof(DataGrid.ItemsSource), new Bind(source.Data));
                if (source.Command != null)
                    b.SetBinding(nameof(DataGrid.DoubleClick), source.BindCommand(_editWith));
            }
        };
    }
    private static Pager CreatePager(FormItem source)
    {
        return new Pager()
        {
            Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind(source.Data))
        };
    }

    private static TextBox CreateTextBox(FormItem source)
    {
        return new TextBox()
        {
            Label = source.Label.Localize(),
            Align = source.ToTextAlign(),
            Width = Length.FromStringNull(source.Width),
            Multiline = source.Props?.Multiline == true,
            Rows = source.Props?.Multiline == true ? 3 : null,
            TabIndex = source.Props?.TabIndex ?? 0, 
            Required = source.Props?.Required == true,
            Highlight = source.Props?.Highlight == true,
            ShowClear = source.Props?.ShowClear == true,
            Bindings = b => b.SetBinding(nameof(TextBox.Value), source.TypedBind())
        };
    }

    private static SelectorSimple CreateSelector(FormItem source, String? param)
    {
        return new SelectorSimple()
        {
            Label = source.Label.Localize(),
            Url = source.Props?.Url,
            Width = Length.FromStringNull(source.Width),
            Placeholder = source.Props?.Placeholder.Localize(),
            ShowClear = source.Props?.ShowClear == true,
            LineClamp = source.Props?.LineClamp ?? 0,
            Required = source.Props?.Required == true,
            Highlight = source.Props?.Highlight == true,
            Folder = source.Props?.Folder == true,
            Bindings = b => b.SetBinding(nameof(SelectorSimple.Value), new Bind(source.Data))
        };
    }
    private static ComboBox CreateComboBox(FormItem source, String? param)
    {
        return new ComboBox()
        {
            Label = source.Label.Localize(),
            Width = Length.FromStringNull(source.Width),
            Required = source.Props?.Required == true,
            Highlight = source.Props?.Highlight == true,
            Bindings = b => { 
                b.SetBinding(nameof(ComboBox.Value), new Bind(source.Data));
                b.SetBinding(nameof(ComboBox.ItemsSource), new Bind(source?.Props?.ItemsSource 
                    ?? throw new InvalidOperationException("ComboBox. ItemsSource is null")));
            },
            Children = [
                new ComboBoxItem() {
                    Bindings = b => {
                        b.SetBinding(nameof(ComboBoxItem.Content), new Bind("Name"));
                        b.SetBinding(nameof(ComboBoxItem.Value), new Bind());
                    }
                }
            ]
        };
    }
    private static ComboBox CreateComboBoxBit(FormItem source, String? param)
    {
        return new ComboBox()
        {
            Label = source.Label.Localize(),
            Width = Length.FromStringNull(source.Width),
            Required = source.Props?.Required == true,
            Highlight = source.Props?.Highlight == true,
            Bindings = b => {
                b.SetBinding(nameof(ComboBox.Value), new Bind(source.Data));
            },
            Children = [
                new ComboBoxItem() { Content = "@[Bit.All]", Value = "" },
                new ComboBoxItem() { Content = "@[Bit.Yes]", Value = "Y" },
                new ComboBoxItem() { Content = "@[Bit.No]", Value = "N" },
            ]
        };
    }

    private static DatePicker CreateDatePicker(FormItem source)
    {
        return new DatePicker()
        {
            Label = source.Label.Localize(),
            CssClass = source.CssClass,
            Width = Length.FromStringNull(source.Width),
            Required = source.Props?.Required == true,
            Bindings = b => b.SetBinding(nameof(DatePicker.Value), new Bind(source.Data))
        };
    }
    private static PeriodPicker CreatePeriodPicker(FormItem source, String? param)
    {
        return new PeriodPicker()
        {
            Label = source.Label.Localize(),
            CssClass = source.CssClass,
            Width = Length.FromStringNull(source.Width),
            Placement = param == "taskpad" ? DropDownPlacement.BottomRight : default,
            Display = DisplayMode.Name,
            Bindings = b =>
            {
                b.SetBinding(nameof(PeriodPicker.Value), new Bind(source.Data));
                b.SetBinding(nameof(PeriodPicker.Description), new Bind($"{source.Data}.Name"));
            }
        };
    }
    private static CheckBox CreateCheckBox(FormItem source)
    {
        return new CheckBox()
        {
            Label = source.Label.Localize(),
            CssClass = source.CssClass,
            Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind(source.Data))
        };
    }

    private Table CreateTable(FormItem source)
    {
        var headers = source.Items?.Select(c => new TableCell() { Content = c.Label.Localize()});

        var cells = source.Items?.Select(c => new TableCell()
        {
            Width = Length.FromStringNull(c.Width),
            Align = c.ToTextAlign(),
            Content = c.Items != null && c.Items.Length > 0 ? CreateElement(c.Items[0], null, "table") : null,
        });

        IEnumerable<TableRow> TotalRows()
        {
            if (source.Items == null)
                yield break;
            var cols = source.Items.ToList();

            List<TableCell> totals = [];
            Int32 span = 0;
            for (Int32 i = 0; i < cols.Count; i++)
            {
                var item = source.Items[i];
                var hasTotal = item.Props?.Total == true;
                if (!hasTotal && totals.Count == 0)
                {
                    span += 1;
                    continue;
                }
                if (span > 0 && totals.Count == 0)
                    totals.Add(new TableCell()
                    {
                        Content = "@[Total]",
                        ColSpan = span
                    });
                totals.Add(new TableCell()
                {
                    Width = Length.FromStringNull(item.Width),
                    Align = item.ToTextAlign(),
                    Bindings = b =>
                    {
                        if (hasTotal)
                            b.SetBinding(nameof(TableCell.Content), item.TypedBind());
                    }
                });
            }

            if (source.Items != null && source.Items.Any(c => c.Props?.Total == true))
                yield return new TableRow()
                {
                    Cells = [.. totals]
                };
        }

        return new Table()
        {
            StickyHeaders = true,
            CssClass = source.CssClass,
            GridLines = GridLinesVisibility.Both,
            Bindings = b => b.SetBinding(nameof(Table.ItemsSource), new Bind(source.Data)),
            Background = TableBackgroundStyle.White,
            Height = Length.FromStringNull(source.Height),
            Header = [
                new TableRow() {
                    Cells = [..headers ?? []]
                }
            ],
            Rows = [
                new TableRow() {
                    Cells = [..cells ?? []]
                }
            ],
            Footer = [..TotalRows()]
        };
    }

    private static Header CreateHeader(FormItem source)
    {
        return new Header()
        {
            Content = source.Label.Localize(),
            Bold = false,
            CssClass = source.CssClass
        };
    }

    private static Label CreateLabel(FormItem source)
    {
        return new Label()
        {
            Content = source.Label.Localize()
        };
    }

    private static Span CreateSpan(FormItem source)
    {
        return new Span()
        {
            Bindings = b => b.SetBinding(nameof(Span.Content), source.TypedBind())
        };
    }

    private static Static CreateStatic(FormItem source)
    {
        return new Static()
        {
            Label = source.Label.Localize(),
            Align = source.ToTextAlign(),
            Width = Length.FromStringNull(source.Width),
            Bindings = b => b.SetBinding(nameof(Static.Value), source.TypedBind())
        };
    }


    private UIElement CreateButton(FormItem source, String? param)
    {
        if (param == "table")
            return new Hyperlink()
            {
                Content = source.Label.Localize(),
                Icon = source.Command2Icon(),
                CssClass = source.CssClass,
                Bindings = b => b.SetBinding(nameof(Button.Command), source.BindCommand(_editWith))
            };
        return new Button()
        {
            Content = source.Label.Localize(),
            CssClass = source.CssClass,
            Bindings = b => b.SetBinding(nameof(Button.Command), source.BindCommand(_editWith))
        };
    }

    private Toolbar? CreateToolbar(FormItem? source)
    {
        if (source == null)
            return null;
        IEnumerable<UIElementBase> Buttons()
        {
            if (source.Items == null)
                return Enumerable.Empty<Button>();

            UIElementBase CreateElement(FormItem c)
            {
                UIElementBase elem = c.Is switch
                {
                    FormItemIs.Button => new Button()
                    {
                        Content = c.Label.Localize(),
                        Icon = c.Command2Icon(),
                        CssClass = c.CssClass,
                        Render = c.Command2RenderMode(),
                        Bindings = b => {
                            b.SetBinding(nameof(Button.Command), c.BindCommand(_editWith));
                        }
                    },
                    FormItemIs.Aligner => new ToolbarAligner(),
                    FormItemIs.Separator => new Separator(),
                    FormItemIs.SearchBox => new SearchBox()
                    {
                        TabIndex = 1,
                        Label = c.Label.Localize(),
                        Placeholder = "@[Search]",
                        CssClass = c.CssClass,
                        Width = Length.FromStringNull(c.Width),
                        Bindings = b => b.SetBinding(nameof(SearchBox.Value), new Bind(c.Data))
                    },
                    _ => throw new InvalidOperationException($"Implement toolbar elem: {c.Is}")
                };
                if (!String.IsNullOrEmpty(c.If))
                    elem.SetBinding(nameof(UIElementBase.If), new Bind(c.If));
                return elem;
            }
            return source.Items.Select(CreateElement);
        }
        return new Toolbar(_xamlServiceProvider)
        {
            CssClass = source.CssClass,
            Children = [.. Buttons()]
        };
    }

    IEnumerable<UIElement> CreateDialogButtons(Form form)
    {
        if (form.Buttons == null)
            return Enumerable.Empty<Button>();
        return form.Buttons.Select(e => new Button()
        {
            Content = e.Label.Localize(),
            Style = e.Props?.Style == ItemStyle.Primary ? ButtonStyle.Primary : ButtonStyle.Default,
            Bindings = b => b.SetBinding(nameof(Button.Command), e.BindCommand(_editWith))
        });
    }

    private Taskpad? CreateTaskpad(FormItem? item, AttachAction? attach = null)
    {
        if (item == null)
            return null;
        return new Taskpad()
        {
            Title = item.Label.Localize(),
            Collapsible = true,
            CssClass = item.CssClass,
            Children = [.. CreateElements(item.Items, attach, "taskpad")],
        };
    }
    private Page BuildPage(Form form)
    {
        var page = new Page()
        {
            CollectionView = CreateCollectionView(form),
            Children = [.. CreateElements(form.Items)],
            Taskpad = CreateTaskpad(form.Taskpad, Attach),
            Toolbar = CreateToolbar(form.Toolbar),
            Title = form.Label.Localize(),
            CssClass = form.CssClass
        };
        return page;
    }

    private Dialog BuildDialog(Form form)
    {
        var dialog = new Dialog()
        {
            Title = form.Label.Localize(),
            CssClass = form.CssClass,
            Overflow = true,
            Width = Length.FromStringNull(form.Width),
            CollectionView = CreateCollectionView(form),
            Children = [.. CreateElements(form.Items)],
            Buttons = [.. CreateDialogButtons(form)],
            Taskpad = CreateTaskpad(form.Taskpad, Attach)
        };
        return dialog;
    }
}

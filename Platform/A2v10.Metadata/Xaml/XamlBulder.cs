// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.System.Xaml;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal class XamlBulder(EditWithMode _withMode)
{
    private readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();
    public static RootContainer BuildForm(Form form, EditWithMode withMode)
    {
        var b = new XamlBulder(withMode);
        return form.Is switch
        {
            FormItemIs.Page => b.BuildPage(form),
            FormItemIs.Dialog => b.BuildDialog(form),
            _ => throw new InvalidOperationException($"Build {form.Is}")
        };
    }

    private CollectionView? CreateCollectionView(Form form)
    {
        if (!form.UseCollectionView)
            return null;

        IEnumerable<FilterItem> Filters()
        {
            yield return new FilterItem() { 
                Property = "Fragment"
            };

            var filters = form.Props?.Filters;
            if (String.IsNullOrEmpty(filters))
                yield break;
            foreach (var f in filters.Split(','))
            {
                if (f == "Period")
                    yield return new FilterItem() { Property = f, DataType = DataType.Period };
                else
                    yield return new FilterItem() { Property = f, DataType = DataType.Object };
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

    private UIElementBase? CreateElement(FormItem item, Action<IDictionary<String, Object>, FormItem>? attach = null, String? param = null) 
    {
        UIElementBase? elem = item.Is switch
        {
            FormItemIs.Grid => CreateGrid(item, param),
            FormItemIs.Panel => CreatePanel(item, param),
            FormItemIs.DataGrid => CreateDataGrid(item),
            FormItemIs.Tabs => CreateTabs(item),
            FormItemIs.Pager => CreatePager(item),
            FormItemIs.Toolbar => CreateToolbar(item),
            FormItemIs.TextBox => CreateTextBox(item),
            FormItemIs.Selector => CreateSelector(item),
            FormItemIs.DatePicker => CreateDatePicker(item),
            FormItemIs.PeriodPicker => CreatePeriodPicker(item, param),
            FormItemIs.CheckBox => CreateCheckBox(item),
            FormItemIs.Table => CreateTable(item),
            FormItemIs.Header => CreateHeader(item),
            FormItemIs.Label => CreateLabel(item),
            FormItemIs.Button => CreateButton(item),
            _ => throw new NotImplementedException($"Implement CreateElement: {item.Is}")
        };
        if (elem != null && attach != null)
            elem.Attach = att => attach(att, item);
        return elem;
    }
    private IEnumerable<UIElementBase> CreateElements(IEnumerable<FormItem>? items, Action<IDictionary<String, Object>, FormItem>? attach = null, String? param = null)
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
                yield return CreateTabsBody(item);
        }
    }

    private UIElementBase CreateTabsBody(FormItem item)
    {
        Case CreateCase(FormItem ch)
        {
            return new Case()
            {
                Value = "",
                Children = [..CreateElements(ch.Items)]
            };
        }
        return new Switch()
        {
            Bindings = b => b.SetBinding(nameof(Switch.Expression), new Bind("Root.$$Tab")),
            Cases = [..item.Items?.Select(CreateCase) ?? []]
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
        AttachInt32("Grid.Row", source.Grid.Row);
        AttachInt32("Grid.Col", source.Grid.Col);
        AttachInt32("Grid.RowSpan", source.Grid.RowSpan);
        AttachInt32("Grid.ColSpan", source.Grid.ColSpan);
    }

    private UIElement CreateTabs(FormItem item)
    {
        var tabBarButtons = item.Items?.Select(c => new TabButton() { Content = c.Label }) ?? [];
        return new TabBar()
        {
            Buttons = [..tabBarButtons],
            Bindings = b => b.SetBinding(nameof(TabBar.Value), new Bind("Root.$$Tab"))
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

    private Grid CreateGrid(FormItem source, String? param)
    {
        IEnumerable<RowDefinition> GridRows()
        {
            if (source.Props?.Rows == null)
                return Enumerable.Empty<RowDefinition>();
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
                return Enumerable.Empty<ColumnDefinition>();
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
            Height = !String.IsNullOrEmpty(source.Height) ? Length.FromString(source.Height) : null,
            Children = [..CreateElements(source.Items, Attach, param)]
        };
    }

    private DataGrid CreateDataGrid(FormItem source)
    {
        IEnumerable<DataGridColumn> Columns()
        {
            if (source.Items == null)
                return Enumerable.Empty<DataGridColumn>();
            return source.Items.Select(c => new DataGridColumn()
            {
                Header = c.Label.Localize(),
                Role = c.ToColumnRole(),
                Align = c.ToTextAlign(),
                SortProperty  = c.Data.EndsWith(".Name") ? c.Data[..^5] : null,
                Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), c.TypedBind())
            });
        }

        return new DataGrid()
        {
            FixedHeader = true,
            Sort = true,
            Columns = [.. Columns()],
            Height = Length.FromStringNull(source.Height),
            Bindings = b =>
            {
                b.SetBinding(nameof(DataGrid.ItemsSource), new Bind(source.Data));
                if (source.Command != null)
                    b.SetBinding(nameof(DataGrid.DoubleClick), source.BindCommand(_withMode));
            }
        };
    }
    private Pager CreatePager(FormItem source)
    {
        return new Pager()
        {
            Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind(source.Data))
        };
    }

    private TextBox CreateTextBox(FormItem source)
    {
        Int32 tabIndex = source.Data.EndsWith(".Name") ? 1 : 0;
        return new TextBox()
        {
            Label = source.Label.Localize(),
            TabIndex = tabIndex,
            Align = source.ToTextAlign(),
            Width = Length.FromStringNull(source.Width),
            Bindings = b => b.SetBinding(nameof(TextBox.Value), source.TypedBind())
        };
    }
    private Selector CreateSelector(FormItem source)
    {
        return new SelectorSimple()
        {
            Label = source.Label.Localize(),
            Url = source.Props?.Url,
            Width = Length.FromStringNull(source.Width),
            Placeholder = source.Props?.Placeholder.Localize(),
            ShowClear = source.Props?.ShowClear == true,
            Bindings = b => b.SetBinding(nameof(SelectorSimple.Value), new Bind(source.Data))
        };
    }
    private DatePicker CreateDatePicker(FormItem source)
    {
        return new DatePicker()
        {
            Label = source.Label.Localize(),
            Width = Length.FromStringNull(source.Width),
            Bindings = b => b.SetBinding(nameof(DatePicker.Value), new Bind(source.Data))
        };
    }
    private PeriodPicker CreatePeriodPicker(FormItem source, String? param)
    {
        return new PeriodPicker()
        {
            Label = source.Label.Localize(),
            Width = Length.FromStringNull(source.Width),
            Placement = param == "taskpad" ? DropDownPlacement.BottomRight : default,
            Bindings = b => b.SetBinding(nameof(PeriodPicker.Value), new Bind(source.Data))
        };
    }
    private CheckBox CreateCheckBox(FormItem source)
    {
        return new CheckBox()
        {
            Label = source.Label.Localize(),
            Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind(source.Data))
        };
    }

    private Table CreateTable(FormItem source)
    {
        var headers = source.Items?.Select(c => new TableCell() { Content = c.Label});
        var cells = source.Items?.Select(c => new TableCell()
        {
            Content = c.Items != null && c.Items.Length > 0 ? CreateElement(c.Items[0]) : null,
        });

        return new Table()
        {
            StickyHeaders = true,
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
            ]
        };
    }

    private Header CreateHeader(FormItem source)
    {
        return new Header()
        {
            Content = source.Label.Localize(),
            Bold = false
        };
    }
    private Label CreateLabel(FormItem source)
    {
        return new Label()
        {
            Content = source.Label.Localize()
        };
    }

    private Button CreateButton(FormItem source)
    {
        return new Button()
        {
            Content = source.Label.Localize(),
            Bindings = b => b.SetBinding(nameof(Button.Command), source.BindCommand(_withMode))
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
                return c.Is switch
                {
                    FormItemIs.Button => new Button()
                    {
                        Content = c.Label.Localize(),
                        Icon = c.Command2Icon(),
                        Bindings = b => b.SetBinding(nameof(Button.Command), c.BindCommand(_withMode))
                    },
                    FormItemIs.Aligner => new ToolbarAligner(),
                    FormItemIs.SearchBox => new TextBox()
                    {
                        ShowClear = true,   
                        ShowSearch = true,  
                        Placeholder = "@[Search]",
                        Width = Length.FromStringNull(c.Width),
                        Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind(c.Data))
                    },
                    _ => throw new InvalidOperationException($"Implement toolbar elem: {c.Is}")
                };
            }
            return source.Items.Select(CreateElement);
        }
        return new Toolbar(_xamlServiceProvider)
        {
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
            Bindings = b => b.SetBinding(nameof(Button.Command), e.BindCommand(_withMode))
        });
    }

    private Taskpad? CreateTaskpad(FormItem? item)
    {
        if (item == null)
            return null;
        return new Taskpad()
        {
            Title = item.Label.Localize(),
            Collapsible = true,
            Children = [.. CreateElements(item.Items, null, "taskpad")],
        };
    }
    private Page BuildPage(Form form)
    {
        var page = new Page()
        {
            CollectionView = CreateCollectionView(form),
            Children = [.. CreateElements(form.Items)],
            Taskpad = CreateTaskpad(form.Taskpad),
            Toolbar = CreateToolbar(form.Toolbar),
            Title = form.Label.Localize(),
        };
        return page;
    }

    private Dialog BuildDialog(Form form)
    {
        var dialog = new Dialog()
        {
            Title = form.Label.Localize(),
            Width = Length.FromStringNull(form.Width),
            CollectionView = CreateCollectionView(form),
            Children = [.. CreateElements(form.Items)],
            Buttons = [.. CreateDialogButtons(form)]
        };
        return dialog;
    }
}

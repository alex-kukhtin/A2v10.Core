// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.System.Xaml;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal class XamlBulder
{
    private readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();
    public static RootContainer BuildForm(Form form)
    {
        var b = new XamlBulder();
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
                yield return new FilterItem() { Property = f, DataType = DataType.Object };
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

    private IEnumerable<UIElementBase> CreateElements(IEnumerable<FormItem>? items, Action<IDictionary<String, Object>, FormItem>? attach = null)
    {
        if (items == null)
            yield break;
        foreach (var item in items)
        {
            UIElementBase elem = item.Is switch
            {
                FormItemIs.Grid => CreateGrid(item),
                FormItemIs.Panel => CreatePanel(item),
                FormItemIs.DataGrid => CreateDataGrid(item),
                FormItemIs.Pager => CreatePager(item),
                FormItemIs.Toolbar => CreateToolbar(item),
                FormItemIs.TextBox => CreateTextBox(item),
                FormItemIs.Selector => CreateSelector(item),
                FormItemIs.DatePicker => CreateDatePicker(item),
                FormItemIs.CheckBox => CreateCheckBox(item),
                _ => throw new NotImplementedException($"Implement CreateElement: {item.Is}")
            };
            if (attach != null)
                elem.Attach = att => attach(att, item);
            yield return elem;
        }
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
        AttachInt32("Grid.RowSpan", source.Grid.ColSpan);
    }

    private Panel CreatePanel(FormItem source)
    {
        return new Panel()
        {
            Header = source.Label.Localize(),
            Collapsible = true,
            Style = PaneStyle.Transparent,
            Children = [.. CreateElements(source.Items, Attach)]
        };
    }

    private Grid CreateGrid(FormItem source)
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
            Children = [..CreateElements(source.Items, Attach)]
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
                    b.SetBinding(nameof(DataGrid.DoubleClick), source.BindCommand());
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
            Url = $"/{source.Props?.Url}",
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
            Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind(source.Data))
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

    private Toolbar CreateToolbar(FormItem source)
    {
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
                        Bindings = b => b.SetBinding(nameof(Button.Command), c.BindCommand())
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
            Bindings = b => b.SetBinding(nameof(Button.Command), e.BindCommand())
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
            Children = [.. CreateElements(item.Items)],
        };
    }
    private Page BuildPage(Form form)
    {
        var page = new Page()
        {
            CollectionView = CreateCollectionView(form),
            Children = [.. CreateElements(form.Items)],
            Taskpad = CreateTaskpad(form.Taskpad),
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

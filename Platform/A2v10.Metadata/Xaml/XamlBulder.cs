
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
        return new CollectionView()
        {
            RunAt = form.Is == FormItemIs.Page ? RunMode.ServerUrl : RunMode.Server,
            Bindings = b => b.SetBinding(nameof(CollectionView.ItemsSource), new Bind(form.Data))
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
                FormItemIs.DataGrid => CreateDataGrid(item),
                FormItemIs.Pager => CreatePager(item),
                FormItemIs.Toolbar => CreateToolbar(item),
                FormItemIs.TextBox => CreateTextBox(item),
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

        AttachInt32("Grid.Row", source.row);
        AttachInt32("Grid.Col", source.col);
        AttachInt32("Grid.RowSpan", source.rowSpan);
        AttachInt32("Grid.RowSpan", source.colSpan);
    }

    private Grid CreateGrid(FormItem source)
    {
        IEnumerable<RowDefinition> GridRows()
        {
            if (source.Rows == null)
                return Enumerable.Empty<RowDefinition>();
            return source.Rows.Split(' ').Select(r =>
                new RowDefinition()
                {
                    Height = GridLength.FromString(r),
                }
            );
        }
        return new Grid(_xamlServiceProvider)
        {
            Rows = [..GridRows()],
            //Columns
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
                Header = c.Label,
                Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), new Bind(c.Data))
            });
        }

        return new DataGrid()
        {
            FixedHeader = true,
            Sort = true,
            Bindings = b => b.SetBinding(nameof(DataGrid.ItemsSource), new Bind(source.Data)),
            Columns = [.. Columns()]
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
        return new TextBox()
        {
            Label = source.Label,
            Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind(source.Data))
        };
    }

    private Toolbar CreateToolbar(FormItem source)
    {
        IEnumerable<Button> Buttons()
        {
            if (source.Items == null)
                return Enumerable.Empty<Button>();

            return source.Items.Select(c => new Button()
            {
                Content = c.Label,
                Icon = c.Command.Command2Icon(),
                Bindings = b => b.SetBinding(nameof(Button.Command), c.BindCommand())
            });
        }
        return new Toolbar(_xamlServiceProvider)
        {
            Children = [..Buttons()]
        };
    }

    private Page BuildPage(Form form)
    {
        var page = new Page()
        {
            CollectionView = CreateCollectionView(form),
            Children = [.. CreateElements(form.Items)]
        };
        return page;
    }

    private Dialog BuildDialog(Form form)
    {
        var dialog = new Dialog()
        {
            CollectionView = CreateCollectionView(form),
            Children = [.. CreateElements(form.Items)]
        };
        return dialog;
    }
}

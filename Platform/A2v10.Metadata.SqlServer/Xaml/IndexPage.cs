// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.Metadata.SqlServer;

internal partial class ModelPageBuilder
{
    UIElement CreateIndexPage(IPlatformUrl platformUrl, IModelView modelView, TableMetadata meta)
    {
        var table = modelView.Meta!.Table;

        DataGridColumnCollection DataGridColumns()
        {
            var columns = new DataGridColumnCollection();
            foreach (var c in meta.RealColumns(modelView.Meta))
            {
                var dgc = new DataGridColumn()
                {
                    Header = $"@[{c.Name}]"
                };
                if (c.MaxLength >= 255)
                    dgc.LineClamp = 2;
                if (c.IsReference)
                    ; //dgc.Content = "Reference";
                else
                    dgc.BindImpl.SetBinding(nameof(DataGridColumn.Content), new Bind(c.Name));
                columns.Add(dgc);
            }
            return columns;
        }

        Button EditButton() 
        {
            return new Button()
            {
                Icon = Icon.Edit
            };
        }

        return new Page()
        {
            CollectionView = new CollectionView()
            {
                RunAt = RunMode.ServerUrl,
                Bindings = b => b.SetBinding(nameof(CollectionView.ItemsSource), new Bind(table))
            },
            Children = [
                new Grid(_xamlSericeProvider)
                {
                    Rows = [
                        new RowDefinition() {Height = GridLength.FromString("Auto")},
                        new RowDefinition() {Height = GridLength.FromString("1*")},
                        new RowDefinition() {Height = GridLength.FromString("Auto")},
                    ],
                    Height = Length.FromString("100%"),
                    Children = [
                        new Toolbar(_xamlSericeProvider) {
                            Children = [
                                EditButton(),
                                new Separator(),
                                new Button() {
                                    Icon = Icon.Reload,
                                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd() {Command = CommandType.Reload})
                                }
                            ]
                        },
                        new DataGrid() {
                            FixedHeader = true,
                            Sort = true,
                            Bindings = b => b.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource")),
                            Columns = DataGridColumns()
                        },
                        new Pager() {
                            Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))
                        }
                    ]
                }
            ]
        };
    }
}

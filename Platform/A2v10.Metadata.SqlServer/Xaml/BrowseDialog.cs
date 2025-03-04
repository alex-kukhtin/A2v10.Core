// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Xaml;
using System;

namespace A2v10.Metadata.SqlServer;

internal partial class ModelPageBuilder
{
    UIElement CreateBrowseDialog(IPlatformUrl platformUrl, IModelView modelView, TableMetadata meta)
    {
        var viewMeta = modelView.Meta ??
            throw new InvalidOperationException("Meta is null");
        var elemName = viewMeta.Table.Singular();


        Button SelectButton() 
        {
            var selectCmd = new BindCmd("Select");
            selectCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("Parent.ItemsSource"));
            return new Button()
            {
                Content = "@[Select]",
                Style = ButtonStyle.Primary,
                Bindings = b => b.SetBinding(nameof(Button.Command), selectCmd)
            };
         }

        DataGridColumnCollection DataGridColumns()
        {
            var columns = new DataGridColumnCollection();
            foreach (var c in meta.RealColumns(viewMeta))
            {
                var dgc = new DataGridColumn()
                {
                    Header = $"@[{c.Name}]"
                };
                dgc.Role = c.ColumnDataType switch
                {
                    ColumnDataType.BigInt => c.IsReference ? ColumnRole.Default : ColumnRole.Id,
                    ColumnDataType.Date or ColumnDataType.DateTime => ColumnRole.Date,
                    ColumnDataType.Currency or ColumnDataType.Float => ColumnRole.Number,
                    ColumnDataType.Boolean => ColumnRole.CheckBox,
                    _ => ColumnRole.Default
                };
                if (c.MaxLength >= 255)
                    dgc.LineClamp = 2;
                if (c.IsReference)
                {
                    dgc.SortProperty = c.Name;
                    var sf = c.IsParent ? "Elem" : string.Empty;
                    dgc.BindImpl.SetBinding(nameof(DataGridColumn.Content), new Bind($"{c.Name}{sf}.Name"));
                }
                else
                    dgc.BindImpl.SetBinding(nameof(DataGridColumn.Content), new Bind(c.Name));
                columns.Add(dgc);
            }
            return columns;
        }

        return new Dialog()
        {
            Title = $"@[{meta.Table.Singular()}.Browse]",
            CollectionView = new CollectionView()
            {
                RunAt = RunMode.Server,
                Bindings = b => b.SetBinding(nameof(CollectionView.ItemsSource), new Bind(meta.Table)),
                Filter = new FilterDescription()
                {
                    Items = [
                        new FilterItem() {
                            Property  = "Fragment",
                            DataType = DataType.String
                        }
                    ]
                }
            },
            Buttons = [
                SelectButton(),
                new Button() {
                    Content = "@[Cancel]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd("Close"))
                }
            ],
            Children = [
                new Grid(_xamlSericeProvider) {
                    Rows = [
                        new RowDefinition() {Height = GridLength.FromString("Auto")},
                        new RowDefinition() {Height = GridLength.FromString("1*")},
                        new RowDefinition() {Height = GridLength.FromString("Auto")},
                    ],
                    Children = [
                        new Toolbar(_xamlSericeProvider) {
                            Children = [
                                new Button() {
                                    Icon = Icon.Reload,
                                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd("Reload"))
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

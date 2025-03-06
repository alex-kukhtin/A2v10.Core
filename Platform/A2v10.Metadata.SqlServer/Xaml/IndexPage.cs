// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Xaml;
using System;

namespace A2v10.Metadata.SqlServer;

internal partial class ModelPageBuilder
{
    UIElement CreateIndexPage(IPlatformUrl platformUrl, IModelView modelView, TableMetadata meta)
    {
        var viewMeta = modelView.Meta
             ?? throw new InvalidOperationException("modelView.Meta is null");
        var table = viewMeta.CurrentTable;

        DataGridColumnCollection DataGridColumns()
        {
            var columns = new DataGridColumnCollection();
            foreach (var c in meta.IndexColumns(modelView.Meta))
            {
                var dgc = new DataGridColumn()
                {
                    Header = c.Header
                };
                dgc.Role = c.Column.ColumnDataType switch
                {
                    ColumnDataType.BigInt => c.Column.IsReference ? ColumnRole.Default :  ColumnRole.Id,
                    ColumnDataType.Date or ColumnDataType.DateTime => ColumnRole.Date,
                    ColumnDataType.Currency or ColumnDataType.Float => ColumnRole.Number,
                    ColumnDataType.Boolean => ColumnRole.CheckBox,
                    _ => ColumnRole.Default
                };
                if (c.Column.MaxLength >= 255)
                    dgc.LineClamp = 2;
                if (c.Column.IsReference)
                {
                    dgc.SortProperty = c.Name;
                    var sf = c.Column.IsParent ? "Elem" : string.Empty;
                    dgc.BindImpl.SetBinding(nameof(DataGridColumn.Content), new Bind($"{c.Name}{sf}.Name"));
                }
                else
                    dgc.BindImpl.SetBinding(nameof(DataGridColumn.Content), new Bind(c.Name));
                columns.Add(dgc);
            }
            return columns;
        }

        Button EditButton() 
        {
            var cmd = viewMeta.Edit == MetaEditMode.Dialog
            ? new BindCmd()
            {
                Command = CommandType.Dialog,
                Action = DialogAction.EditSelected,
                Url = $"/{platformUrl.LocalPath}/edit"
            }
            : new BindCmd()
            {
                Command = CommandType.OpenSelected,
                Url = $"/{platformUrl.LocalPath}/edit"
            };
            cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("Parent.ItemsSource"));
            return new Button()
            {
                Icon = Icon.Edit,
                Bindings = b => b.SetBinding(nameof(Button.Command), cmd)
            };
        }

        Button CreateButton()
        {
            var cmd = new BindCmd()
            {
                Command = CommandType.Dialog,
                Action = DialogAction.Append,
                Url = $"/{platformUrl.LocalPath}/edit"
            };
            cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("Parent.ItemsSource"));
            return new Button()
            {
                Icon = Icon.Plus,
                Content = "@[Create]",
                Bindings = b => b.SetBinding(nameof(Button.Command), cmd)
            };
        }

        return new Page()
        {
            CollectionView = new CollectionView()
            {
                RunAt = RunMode.ServerUrl,
                Bindings = b => b.SetBinding(nameof(CollectionView.ItemsSource), new Bind(table)),
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
                                CreateButton(),
                                EditButton(),
                                new Separator(),
                                new Button() {
                                    Icon = Icon.Reload,
                                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd() {Command = CommandType.Reload})
                                },
                                new ToolbarAligner(),
                                new TextBox() {
                                    Placeholder = "@[Search]",
                                    Width = Length.FromString("20rem"),
                                    Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind("Parent.Filter.Fragment"))
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

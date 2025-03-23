// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class ModelPageBuilder
{
    UIElement CreateIndexPage(IPlatformUrl platformUrl, IModelView modelView, FormOld form)
    {
        var viewMeta = modelView.Meta
             ?? throw new InvalidOperationException("modelView.Meta is null");
        var table = viewMeta.CurrentTable;

        Button EditButton() 
        {
            var cmd = viewMeta.EditMode == MetaEditMode.Dialog
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
            Title = form.Title,
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
                            Columns = [..form.IndexColumns()]
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

// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    IEnumerable<DataGridColumn> IndexColumnsXaml(List<TableColumn> columns, Boolean hasChecked) =>
        columns.Select(col =>
            new DataGridColumn()
            {
                Header = col.Header,
                Role = col.Type.ToXamlColumnRole(),
                SortProperty = col.IsRef ? col.Name : null,
                Bindings = b => b.SetBinding(nameof(DataGridColumn.Content),
                    new Bind(col.DisplayPath) { DataType = col.Type.ToXamlDataType() })
            }
         );

    IEnumerable<FilterItem> CollectionViewFilters()
    {
        yield return new FilterItem()
        {
            Property = "Fragment",
            DataType = DataType.String
        };
        foreach (var f in Table.TableFilters())
            yield return f.Type switch
            {
                ColumnType.Date => new FilterItem() { Property = "Period", DataType = DataType.Period },
                _ => new FilterItem() { Property = f.Name, DataType = DataType.Object }
            };
    }

    CollectionView XamlCollectionView() =>
        new()
        {
            RunAt = RunMode.Server,
            Bindings = b => b.SetBinding(nameof(CollectionView.ItemsSource), new Bind(Table.CollectionName)),
            Filter = new FilterDescription()
            {
                Items = [.. CollectionViewFilters()]
            }
        };


    internal UIElement CreateXamlContainer(String action)
    {
        return action switch
        {
            "index" => CreateIndexPageXaml(Table.IndexForm()),
            "browse" => CreateBrowseDialogXaml(Table.BrowseForm()),
            "edit" => CreateEditXaml(Table.EditForm()),
            _ => throw new InvalidOperationException($"Invalid action: '{action}'")
        };
    }

    internal Page CreateIndexPageXaml(FormMetadata meta)
    {
        return new Page()
        {
            CollectionView = XamlCollectionView(),
            Children = [
                new Grid(_xamlServiceProvider) {
                    Rows = RowDefinitions.FromString("Auto,1*,Auto"),
                    Height = Length.FromString("100%"),
                    Children = [..meta.Body.Select(ElementToControl)]
                }
            ],
            Taskpad = ElementToControl(meta.TaskPad)
        };
    }

    internal Partial CreateIndexPagePartialXaml()
    {
        var form = Table.IndexForm();
        var collView = XamlCollectionView();
        collView.Children = [IndexPageGrid(form)];
        return new Partial()
        {
            Children = [collView]
        };
    }

    internal Grid IndexPageGrid(FormMetadata meta)
    {
        UIElementBase CreateIndexToolbar(FormElement? tb)
        {
            return new Toolbar(_xamlServiceProvider);
        }

        return new Grid(_xamlServiceProvider)
        {
            Rows = RowDefinitions.FromString("Auto,1*,Auto"),
            Height = Length.FromString("100%"),
            Children = [
                CreateIndexToolbar(meta.Toolbar),
                //CreateIndexDataGrid(meta.Body[0]),
                new Pager() 
                {
                    Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))
                }
            ]
        };
    }

    internal Taskpad? IndexTaskpad(FormElement taskPad)
    {
        if (taskPad.Fields.Count == 0)
            return null;
        return new Taskpad()
        {
            Children = [
                new Panel() {
                    Header = "@[Filters]",
                    Collapsible = true,
                    Style = PaneStyle.Transparent,
                    //Children = [..taskPad.Filters.Select(CreateFilterControl)]
                },
            ]
        };
    }
    internal Dialog CreateBrowseDialogXaml(FormMetadata dialog)
    {
        var selectCommand = new BindCmd() { Command = CommandType.Select };
        selectCommand.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("Parent.ItemsSource"));
        return new Dialog()
        {
            CollectionView = XamlCollectionView(),
            //Width = Length.FromString(dialog.TaskPad?.Filters.Count > 0 ? "80rem" : "60rem"), // TODO
            Height = Length.FromString("40rem"),
            Title = $"@[{Table.Model}.Browse]",
            Buttons = [
                new Button()
                {
                    Style = ButtonStyle.Primary,
                    Content = "@[Select]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), selectCommand)
                },
                new Button()
                {
                    Content = "@[Cancel]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd() {Command = CommandType.Close })
                },
            ],
            Children = [
                new Grid(_xamlServiceProvider)
                {
                    Rows = RowDefinitions.FromString("Auto,1*,Auto"),
                    Height = Length.FromString("100%"),
                    Children = [
                        ..dialog.Body.Select(ElementToControl),
                        new Pager()
                        {
                            Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))
                        }
                    ]
                }
            ],
            Taskpad = IndexTaskpad(dialog.TaskPad)
        };
    }
}

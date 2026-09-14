// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    /* An index grid shows columns only: the tags of a row are spliced into Name below, not a member.
     *
     * The table is a parameter and not 'Table' - the tags splice is a question about the table these
     * COLUMNS belong to, and the transactions dialog draws the columns of a journal while standing
     * on a document endpoint. Read off the endpoint it answered for the wrong table.
     */
    IEnumerable<DataGridColumn> IndexColumnsXaml(TableMetadata table, List<MemberDescriptor> members) =>
        members.Select(m => m.ColumnCheck).Select(col =>
            table.HasTags && col.Type == ColumnType.Name
            ? new DataGridColumn()
            {
                Header = col.Header,
                SortProperty = col.Name,
                Content = new Group()
                {
                    Children = [
                        new Span() 
                        {
                            Block = true,
                            Bindings = b => b.SetBinding(nameof(DataGridColumn.Content),
                            new Bind(col.DisplayPath) { DataType = col.Type.ToXamlDataType() })
                        },
                        new TagsList() {
                            Bindings = b => b.SetBinding(nameof(TagsList.ItemsSource),
                                new Bind("Tags")),
                        }
                    ]
                }
            }
            : new DataGridColumn()
            {
                Header = col.Header,
                Role = col.Type.ToXamlColumnRole(),
                /* No sort on an enum: neither its Order nor its Name is the alphabet the cell
                 * shows. Said with Sort and not by leaving SortProperty empty - an empty one is
                 * filled from the binding path at init, so that would have changed the sort key
                 * rather than removed the sort.
                 */
                Sort = col.IsEnum ? false : null,
                SortProperty = col.IsRef ? col.Name : null,
                Bindings = b => b.SetBinding(nameof(DataGridColumn.Content),
                    new Bind(col.DisplayPath) { DataType = col.Type.ToXamlDataType() })
            }
         );

    // the ENDPOINT's filters, not the form's - see CLAUDE.md, "Filters"
    IEnumerable<FilterItem> CollectionViewFilters()
    {
        yield return new FilterItem()
        {
            Property = "Fragment",
            DataType = DataType.String
        };
        foreach (var f in Table.Filters())
            yield return f.Kind switch
            {
                FilterKind.Period => new FilterItem() { Property = f.Name, DataType = DataType.Period },
                FilterKind.Tags => new FilterItem() { Property = f.Name, DataType = DataType.String },
                // the code itself, not an object - see the ComboBox in ControlsXaml
                FilterKind.Enum => new FilterItem() { Property = f.Name, DataType = DataType.String },
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
            "index" => CreateIndexPageXaml(Declaration.Form(Constants.FormNames.Index)),
            "indexpartial" => CreateIndexPartialPageXaml(Declaration.Form(Constants.FormNames.Index)),
            "browse" => CreateBrowseDialogXaml(Declaration.Form(Constants.FormNames.Browse)),
            "edit" => CreateEditXaml(Declaration.Form(Constants.FormNames.Edit)),
            // no form: what it shows is derived from 'post' - see TransDialogXaml
            Constants.Trans.Action => CreateTransDialogXaml(),
            // no form
            Constants.Print.Action => CreatePrintPageXaml(),
            _ => throw new InvalidOperationException($"Invalid action: '{action}'")
        };
    }

    /* A row per child, the grid taking the slack: the standard bar first, then the body as written.
     * A body toolbar is a second bar of the author's own commands, one more 'Auto'.
     */
    static RowDefinitions GridRows(IEnumerable<FormElement> body, Boolean pager = false)
    {
        var rows = body.Select(e => e.Is == FormElementKind.DataGrid ? "1*" : "Auto").Prepend("Auto");
        return RowDefinitions.FromString(String.Join(",", pager ? rows.Append("Auto") : rows));
    }

    Grid IndexGrid(FormMetadata meta) =>
        new(_xamlServiceProvider)
        {
            Rows = GridRows(meta.Body),
            Height = Length.FromString("100%"),
            Children = [IndexToolbar(meta.Toolbar), ..meta.Body.Select(ElementToControl)]
        };

    internal Page CreateIndexPageXaml(FormMetadata meta)
    {
        return new Page()
        {
            CollectionView = XamlCollectionView(),
            Children = [IndexGrid(meta)],
            Taskpad = ElementToControl(meta.Taskpad)
        };
    }

    internal Partial CreateIndexPartialPageXaml(FormMetadata meta)
    {
        var collView = XamlCollectionView();
        collView.Children.Add(IndexGrid(meta));
        return new Partial()
        {
            Children = [
                collView
            ],
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
        var dlg = new Dialog()
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
                    Rows = GridRows(dialog.Body, pager: true),
                    Height = Length.FromString("100%"),
                    Children = [
                        BrowseToolbar(dialog.Toolbar),
                        ..dialog.Body.Select(ElementToControl),
                        new Pager()
                        {
                            Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))
                        }
                    ]
                }
            ],
            Taskpad = IndexTaskpad(dialog.Taskpad)
        };

        // by type, not by position: the body may carry a bar of its own before the grid
        if (dlg.Children[0] is Grid chGrid && chGrid.Children.OfType<DataGrid>().FirstOrDefault() is { } dataGrid)
            dataGrid.BindImpl.SetBinding(nameof(DataGrid.DoubleClick), selectCommand);
        return dlg;
    }
}

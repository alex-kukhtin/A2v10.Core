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
    IEnumerable<DataGridColumn> IndexColumnsXaml(TableMetadata table, List<MemberDescriptor> members)
    {
        /* The name as a cell draws it. A row carrying a colour of its own is PAINTED by it - the
         * same badge a state gets, with the style read from this row instead of a referenced one.
         * The tags line is untouched by that: a record may carry both, and they are two facts.
         */
        UIElementBase NameContent(TableColumn col, TableColumn? paint) => paint == null
            ? new Span()
            {
                Block = true,
                Bindings = b => b.SetBinding(nameof(DataGridColumn.Content),
                    new Bind(col.DisplayPath) { DataType = col.Type.ToXamlDataType() })
            }
            : new TagLabel()
            {
                Outline = true,
                Bindings = b =>
                {
                    b.SetBinding(nameof(TagLabel.Content), new Bind(col.DisplayPath));
                    b.SetBinding(nameof(TagLabel.Style), new Bind(paint.DisplayPath));
                }
            };

        DataGridColumn Name(TableColumn col, TableColumn? paint) => new()
        {
            Header = col.Header,
            SortProperty = col.Name,
            Content = NameContent(col, paint)
        };

        DataGridColumn WithTags(TableColumn col, TableColumn? paint) => new()
        {
            Header = col.Header,
            SortProperty = col.Name,
            Content = new Group()
            {
                Children = [
                    NameContent(col, paint),
                    new TagsList() {
                        Bindings = b => b.SetBinding(nameof(TagsList.ItemsSource),
                            new Bind("Tags")),
                    }
                ]
            }
        };

        /* A referenced row is drawn in ITS colour, not spelled - the same badge the picker offers,
         * so a row and the panel read as one thing. Style is a BINDING: TagLabel then emits
         * ':class' and every row takes its own colour, where a static Style would paint the whole
         * column alike.
         *
         * WHICH reference draws this way is the target's answer and not the column's type - a
         * state and a catalog that declared a colour arrive at this one branch, because the map
         * sent a colour for both (SqlBuilder.RefFields). Sorting follows the general rule for a
         * reference: a set's Name is a resource key and not the alphabet the cell shows, a
         * catalog's Name is.
         */
        DataGridColumn Badge(TableColumn col, TableColumn color) => new()
        {
            Header = col.Header,
            Sort = col.IsSetRef ? false : null,
            SortProperty = col.IsSetRef ? null : col.Name,
            Content = new TagLabel()
            {
                Outline = true,
                Bindings = b =>
                {
                    /* Nothing referenced, nothing drawn: without this an empty reference renders
                     * an empty badge, and a frame around no text reads as a control that failed
                     * rather than as a blank cell. Asked of the Id, which is what 'a reference
                     * holds a row' means - a name may legitimately be empty.
                     */
                    b.SetBinding(nameof(TagLabel.If), new Bind($"{col.Name}.{Constants.FieldNames.Id}"));
                    b.SetBinding(nameof(TagLabel.Content), new Bind(col.DisplayPath));
                    b.SetBinding(nameof(TagLabel.Style), new Bind($"{col.Name}.{color.Name}"));
                }
            }
        };

        /* A colour column that is SHOWN draws itself: style and text are the one value, so the cell
         * reads 'green' in green. Reached only where nothing is painted by it - a table with no
         * name to paint, or with two colours. Sorted by the stored name, unlike a set's: this one
         * is what is written in the column and not a resource key.
         */
        DataGridColumn ColorCell(TableColumn col) => new()
        {
            Header = col.Header,
            SortProperty = col.Name,
            Content = new TagLabel()
            {
                Outline = true,
                Bindings = b =>
                {
                    b.SetBinding(nameof(TagLabel.Content), new Bind(col.DisplayPath));
                    b.SetBinding(nameof(TagLabel.Style), new Bind(col.DisplayPath));
                }
            }
        };

        DataGridColumn Plain(TableColumn col) => new()
        {
            Header = col.Header,
            Role = col.Type.ToXamlColumnRole(),
            /* No sort on a set: neither its Order nor its Name is the alphabet the cell
             * shows. Said with Sort and not by leaving SortProperty empty - an empty one is
             * filled from the binding path at init, so that would have changed the sort key
             * rather than removed the sort.
             *
             * The key is withheld with it, and this changes nothing at run time - it is a tidy-up,
             * not a fix. A set is a reference, so the two lines used to put 'do not sort' and a
             * sort key on one column; the key is simply unread there, and an absent one is filled
             * from the binding path at init anyway. Removed because two attributes of one column
             * must not say different things - the next reader would have to find out which wins.
             */
            Sort = col.IsSetRef ? false : null,
            SortProperty = col.IsRef && !col.IsSetRef ? col.Name : null,
            Bindings = b => b.SetBinding(nameof(DataGridColumn.Content),
                new Bind(col.DisplayPath) { DataType = col.Type.ToXamlDataType() })
        };

        var columns = members.Select(m => m.ColumnCheck).ToList();

        /* The row's own colour, put on its NAME: the colour is HOW the name draws and not a column
         * of its own - 'green' beside a green name is one fact written twice, and the tags splice
         * is the same act. The same colour the map sends to everyone referencing this row, so the
         * grid here and the badge there cannot show different things.
         */
        var paint = columns.Any(c => c.Type == ColumnType.Name) ? table.ColorColumn : null;

        return columns.Where(col => col != paint).Select(col =>
            table.HasTags && col.Type == ColumnType.Name ? WithTags(col, paint)
            : col.Type == ColumnType.Name && paint != null ? Name(col, paint)
            : col.RefTable?.Storage.ColorColumn is { } color ? Badge(col, color)
            : col.Type == ColumnType.Color ? ColorCell(col)
            : Plain(col));
    }

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
                FilterKind.Set or FilterKind.Role => new FilterItem() { Property = f.Name, DataType = DataType.String },
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
        var rows = body.Select(e => e.Is is FormElementKind.DataGrid or FormElementKind.TreeGrid ? "1*" : "Auto").Prepend("Auto");
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
            // what a DataGrid reads through (Parent.ItemsSource, Parent.Pager); a tree is bound to its collection
            CollectionView = meta.Body.Any(e => e.Is == FormElementKind.DataGrid) ? XamlCollectionView() : null,
            Children = [IndexGrid(meta)],
            Taskpad = ElementToControl(meta.Taskpad) is Taskpad { Children.Count: > 0 } taskpad ? taskpad : null
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

    internal Dialog CreateBrowseDialogXaml(FormMetadata dialog)
    {
        // a grid reads through the collection view and pages; a tree is bound to its collection, read whole
        var isGrid = dialog.Body.Any(e => e.Is == FormElementKind.DataGrid);
        var selectCommand = new BindCmd() { Command = CommandType.Select };
        selectCommand.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(isGrid ? "Parent.ItemsSource" : Table.CollectionName));
        var dlg = new Dialog()
        {
            CollectionView = isGrid ? XamlCollectionView() : null,
            Width = Length.FromString(dialog.Taskpad.Elements.Count > 0 ? "80rem" : "60rem"), // TODO: calculate width from columns
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
                    Rows = GridRows(dialog.Body, pager: isGrid),
                    Height = Length.FromString("100%"),
                    Children = [
                        BrowseToolbar(dialog.Toolbar),
                        ..dialog.Body.Select(ElementToControl),
                        ..(isGrid ? new UIElementBase[] { new Pager()
                        {
                            Bindings = b => b.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))
                        } } : [])
                    ]
                }
            ],
            Taskpad = ElementToControl(dialog.Taskpad) is Taskpad { Children.Count: > 0 } taskpad ? taskpad : null
        };

        // by type, not by position: the body may carry a bar of its own before the grid
        if (dlg.Children[0] is Grid chGrid && chGrid.Children.OfType<DataGrid>().FirstOrDefault() is { } dataGrid)
            dataGrid.BindImpl.SetBinding(nameof(DataGrid.DoubleClick), selectCommand);
        if (dlg.Children[0] is Grid trGrid && trGrid.Children.OfType<TreeGrid>().FirstOrDefault() is { } treeGrid)
            treeGrid.BindImpl.SetBinding(nameof(TreeGrid.DoubleClick), selectCommand);
        return dlg;
    }
}

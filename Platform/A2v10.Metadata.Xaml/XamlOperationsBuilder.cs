// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.System.Xaml;
using A2v10.Xaml;

namespace A2v10.Metadata;

/* The browse dialogs of the platform's own reference targets - the operations, the logins. Fetched
 * whole, so what they lack is what such a list has no use for: pager, search, taskpad, command bar.
 */
internal class XamlOperationsBuilder
{
    private readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    public Dialog RenderBrowseDialog() =>
        BrowseDialog(TableMetadataDefaults.OperationsTable(), ("@[Name]", "Name"), ("@[Memo]", "Memo"));

    // the login, beside the name: two people of one name are told apart by it
    public Dialog RenderUsersDialog() =>
        BrowseDialog(TableMetadataDefaults.UsersTable(), ("@[Name]", "Name"), ("@[Admin.Login]", "UserName"));

    /* The same three parts the generated browse dialog has - a CollectionView, a grid inside it,
     * and a Select button carrying the current row - because the selector on the other side reads
     * exactly those. The first column sorts.
     */
    private Dialog BrowseDialog(TableMetadata table, params (String Header, String Path)[] columns)
    {
        var selectCommand = new BindCmd() { Command = CommandType.Select };
        selectCommand.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(table.CollectionName));

        var grid = new DataGrid()
        {
            FixedHeader = true,
            Sort = true,
            Bindings = b =>
            {
                b.SetBinding(nameof(DataGrid.ItemsSource), new Bind(table.CollectionName));
                b.SetBinding(nameof(DataGrid.DoubleClick), selectCommand);
            },
            Columns = [.. columns.Select((c, ix) => new DataGridColumn()
            {
                Header = c.Header,
                SortProperty = ix == 0 ? c.Path : null,
                Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), new Bind(c.Path))
            })]
        };

        return new Dialog()
        {
            Title = $"@[{table.Model}.Browse]",
            Width = Length.FromString("40rem"),
            Height = Length.FromString("32rem"),
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
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.Close))
                }
            ],
            Children = [
                new Grid(_xamlServiceProvider)
                {
                    Height = Length.FromString("100%"),
                    Children = [grid]
                }
            ]
        };
    }
}

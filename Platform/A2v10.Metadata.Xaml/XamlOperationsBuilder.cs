// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.System.Xaml;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal class XamlOperationsBuilder
{
    private readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    /* The same three parts the generated browse dialog has - a CollectionView, a grid inside it,
     * and a Select button carrying the current row - because the selector on the other side reads
     * exactly those. What is missing is what an operation registry has no use for: pager, search,
     * taskpad, command bar.
     */
    public Dialog RenderBrowseDialog()
    {
        var table = TableMetadataDefaults.OperationsTable();

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
            Columns = [
                new DataGridColumn() {
                    Header = "@[Name]",
                    SortProperty = "Name",
                    Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), new Bind("Name"))
                },
                new DataGridColumn() {
                    Header = "@[Memo]",
                    Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), new Bind("Memo"))
                }
            ]
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

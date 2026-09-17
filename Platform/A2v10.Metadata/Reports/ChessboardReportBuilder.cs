// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Xaml;

namespace A2v10.Metadata;

/* The chessboard of a ledger: debit accounts down, credit accounts across, the turnover of the period
 * where they meet. Read from the debit legs alone - every posting is there once, as Acc with its
 * CorrAcc - so a row total is the debit turnover of the account and a column total its credit one.
 *
 * The columns are data: each row carries its cells as a cross array keyed by the corresponding
 * account's code, and the code is the key of the chart, so no join is needed to name a column.
 */
internal class ChessboardReportBuilder(IServiceProvider serviceProvider, ReportMetadata report, TableMetadata source, AppPlatformId platformId)
    : LedgerReportBuilder(serviceProvider, report, source, platformId)
{
    protected override String CreateBodySql()
    {
        var acc = Constants.FieldNames.Acc;
        var corrAcc = Constants.FieldNames.CorrAcc;
        var sum = Constants.FieldNames.Sum;
        var legs = $"""
            from {_source.SqlTableName} j
            where @Run = 1 and j.[{Constants.FieldNames.InOut}] = 1 and j.[Date] >= @From and j.[Date] < @end{_grouping.SqlWhereClause("j")}
            """;

        return $"""
        with T as (
            select j.[{acc}], [Sum] = sum(j.[{sum}]), [Grp] = grouping(j.[{acc}])
            {legs}
            group by rollup(j.[{acc}])
        )
        select [RepData!TRepData!Group] = null, [Id!!Id] = T.[{acc}],
            [Name] = a.[Name], T.[Sum],
            [{acc}!!GroupMarker] = T.[Grp],
            [Cross!TCross!CrossArray] = null,
            [Items!TRepData!Items] = null
        from T left join {ChartTableName} a on a.[Id] = T.[{acc}]
        where @Run = 1
        order by T.[Grp] desc, T.[{acc}];

        select [!TCross!CrossArray] = null, [Key!!Key] = j.[{corrAcc}],
            [Sum] = sum(j.[{sum}]), [!TRepData.Cross!ParentId] = j.[{acc}]
        {legs}
        group by j.[{acc}], j.[{corrAcc}]
        order by j.[{corrAcc}];
        """;
    }

    // the column totals: per key of the cross, the sum of that cell over the rows
    protected override String TemplateProperties => "'TRepDataArray.$CrossTotals': crossTotals";

    protected override String TemplateFunctions => """
        function crossTotals() {
            return this.$cross.Cross.map(x => {
                return {
                    Sum: this.reduce((prev, curr) =>
                        prev + curr.Cross.find(ci => ci.Key === x).Sum, 0)
                };
            });
        }
        """;

    /* The layout of the hand-written chessboard: code, name over two columns, a column per
     * corresponding account, the row total on a yellow column; the column totals come from the
     * model ($CrossTotals of the rows), the grand total from the total row.
     */
    public override UIElement CreatePage()
    {
        static Bind Sum(String path) => new BindSum(path);

        return new Page()
        {
            Title = _report?.ItemLabel.Localize() ?? "@[Chessboard]",
            CssClass = "report-page",
            UserSelect = true,
            Background = BackgroundStyle.White,
            Toolbar = CreateToolbar(),
            Taskpad = CreateTaskpad(),
            Children = [
                new Block() {
                    CssClass = "sheet-page sheet-report",
                    Children = [
                        new Sheet()
                        {
                            GridLines = GridLinesVisibility.Both,
                            Wrap = WrapMode.NoWrap,
                            Width = Length.FromString("1rem"),
                            Bindings = b => {
                                b.SetBinding(nameof(Sheet.If), new Bind("Filter.Run"));
                                b.SetBinding(nameof(Sheet.Stale), new Bind("Root.$AlertVisible"));
                            },
                            Columns = [
                                new SheetColumn() { Fit = true },
                                new SheetColumn() { Width = Length.FromString("3rem") },
                                new SheetColumn(),
                                new SheetColumnGroup() {
                                    Columns = { new SheetColumn() { Width = Length.FromString("Auto") } },
                                    Bindings = b => b.SetBinding(nameof(SheetColumnGroup.ItemsSource), new Bind("RepData.Cross"))
                                },
                                new SheetColumn() { Fit = true, Background = ColumnBackgroundStyle.Yellow }
                            ],
                            Header = [..CreateSheetHeader()],
                            Sections = [
                                new SheetSection() {
                                    Children = [
                                        new SheetRow() {
                                            Style = RowStyle.Header,
                                            Wrap = WrapMode.NoWrap,
                                            Cells = [
                                                new SheetCell() { ColSpan = 3, Content = "@[Chessboard.Header]" },
                                                new SheetCellGroup() {
                                                    Cells = {
                                                        new SheetCell() {
                                                            Align = TextAlign.Center,
                                                            Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Key"))
                                                        }
                                                    },
                                                    Bindings = b => b.SetBinding(nameof(SheetCellGroup.ItemsSource), new Bind("RepData.Cross"))
                                                },
                                                new SheetCell() { Content = "@[Total]" }
                                            ]
                                        }
                                    ]
                                },
                                new SheetSection() {
                                    Children = [
                                        new SheetRow() {
                                            Style = RowStyle.Total,
                                            Align = TextAlign.Right,
                                            Wrap = WrapMode.NoWrap,
                                            Cells = [
                                                new SheetCell() { Align = TextAlign.Left, ColSpan = 3, Content = "@[Total]" },
                                                new SheetCellGroup() {
                                                    Cells = {
                                                        new SheetCell() {
                                                            Align = TextAlign.Right,
                                                            Bindings = b => b.SetBinding(nameof(SheetCell.Content), Sum("Sum"))
                                                        }
                                                    },
                                                    Bindings = b => b.SetBinding(nameof(SheetCellGroup.ItemsSource), new Bind("RepData.Items.$CrossTotals"))
                                                },
                                                new SheetCell() {
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), Sum("RepData.Sum"))
                                                }
                                            ]
                                        }
                                    ]
                                },
                                new SheetSection() {
                                    Bindings = b => b.SetBinding(nameof(SheetSection.ItemsSource), new Bind("RepData.Items")),
                                    Children = [
                                        new SheetRow() {
                                            Wrap = WrapMode.NoWrap,
                                            Cells = [
                                                new SheetCell() {
                                                    Align = TextAlign.Center,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Id"))
                                                },
                                                new SheetCell() {
                                                    ColSpan = 2,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Name"))
                                                },
                                                new SheetCellGroup() {
                                                    Cells = {
                                                        new SheetCell() {
                                                            Align = TextAlign.Right,
                                                            Bindings = b => b.SetBinding(nameof(SheetCell.Content), Sum("Sum"))
                                                        }
                                                    },
                                                    Bindings = b => b.SetBinding(nameof(SheetCellGroup.ItemsSource), new Bind("Cross"))
                                                },
                                                new SheetCell() {
                                                    Align = TextAlign.Right,
                                                    Bold = true,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), Sum("Sum"))
                                                }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                },
                CreateNonRunPanel()
            ]
        };
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

/* The trial balance of a ledger: per account, the opening balance, the debit and credit turnover of
 * the period, the closing balance - each balance as a debit/credit pair.
 *
 * A balance is laid out by the account's NormalBalance, not by its sign: Debit keeps it in the debit
 * column even when negative (a minus on 281 is a visible error, moved to credit it would be a
 * plausible number), Credit mirrors that, Both goes by sign. It is laid out per account and only then
 * summed - the signed total of a balanced ledger is zero, so the total row is the sum of the pairs.
 */
internal class TrialBalanceReportBuilder(IServiceProvider serviceProvider, ReportMetadata report, TableMetadata source, AppPlatformId platformId)
    : LedgerReportBuilder(serviceProvider, report, source, platformId)
{
    // the pair a balance is laid out into, by the side the account keeps it on
    private static String Debit(String balance) => $"""
        case a.[{Constants.FieldNames.NormalBalance}] when N'{nameof(NormalBalance.Debit)}' then {balance}
            when N'{nameof(NormalBalance.Credit)}' then 0 else iif({balance} > 0, {balance}, 0) end
        """;
    private static String Credit(String balance) => $"""
        case a.[{Constants.FieldNames.NormalBalance}] when N'{nameof(NormalBalance.Debit)}' then 0
            when N'{nameof(NormalBalance.Credit)}' then -{balance} else iif({balance} < 0, -{balance}, 0) end
        """;

    protected override String CreateBodySql()
    {
        var acc = Constants.FieldNames.Acc;
        var sum = Constants.FieldNames.Sum;
        var inOut = Constants.FieldNames.InOut;

        return $"""
        with J as (
            select j.[{acc}],
                [Start] = sum(case when j.[Date] < @From then j.[{sum}] * j.[{inOut}] else 0 end),
                [DtSum] = sum(case when j.[Date] >= @From and j.[{inOut}] = 1 then j.[{sum}] else 0 end),
                [CtSum] = sum(case when j.[Date] >= @From and j.[{inOut}] = -1 then j.[{sum}] else 0 end),
                [End] = sum(j.[{sum}] * j.[{inOut}])
            from {_source.SqlTableName} j
            where @Run = 1 and j.[Date] < @end{_grouping.SqlWhereClause("j")}
            group by j.[{acc}]
        ),
        S as (
            select J.[{acc}], J.[DtSum], J.[CtSum],
                [DtStart] = {Debit("J.[Start]")},
                [CtStart] = {Credit("J.[Start]")},
                [DtEnd] = {Debit("J.[End]")},
                [CtEnd] = {Credit("J.[End]")}
            from J inner join {ChartTableName} a on a.[Id] = J.[{acc}]
        ),
        T as (
            select [{acc}], [Grp] = grouping([{acc}]),
                [DtStart] = sum([DtStart]), [CtStart] = sum([CtStart]),
                [DtSum] = sum([DtSum]), [CtSum] = sum([CtSum]),
                [DtEnd] = sum([DtEnd]), [CtEnd] = sum([CtEnd])
            from S
            group by rollup([{acc}])
        )
        select [RepData!TRepData!Group] = null, [Id!!Id] = T.[{acc}],
            [Name] = a.[Name],
            T.[DtStart], T.[CtStart], T.[DtSum], T.[CtSum], T.[DtEnd], T.[CtEnd],
            [{acc}!!GroupMarker] = T.[Grp],
            [Items!TRepData!Items] = null
        from T left join {ChartTableName} a on a.[Id] = T.[{acc}]
        where @Run = 1
        order by T.[Grp] desc, T.[{acc}];
        """;
    }

    /* The layout of the hand-written trial balance: code, name over two columns, three debit/credit
     * pairs on gray-banded columns; a flat section, one row per account, the total row above it.
     */
    public override UIElement CreatePage()
    {
        static SheetCell Sum(String path) => new()
        {
            Bindings = b => b.SetBinding(nameof(SheetCell.Content), new BindSum(path))
        };

        static SheetCell Text(String content, Int32 colSpan = 1) => new()
        {
            Content = content,
            ColSpan = colSpan
        };

        String[] pairs = ["DtStart", "CtStart", "DtSum", "CtSum", "DtEnd", "CtEnd"];

        return new Page()
        {
            Title = _report?.ItemLabel.Localize() ?? "@[TrialBalance]",
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
                            Bindings = b => {
                                b.SetBinding(nameof(Sheet.If), new Bind("Filter.Run"));
                                b.SetBinding(nameof(Sheet.Stale), new Bind("Root.$AlertVisible"));
                            },
                            Columns = [.. "Fit,Fit,Auto,Auto+A,Auto+A,Auto,Auto,Auto+A,Auto+A".Split(',').Select(c => new SheetColumn(c))],
                            Header = [..CreateSheetHeader()],
                            Sections = [
                                new SheetSection() {
                                    Children = [
                                        new SheetRow() {
                                            Style = RowStyle.Header,
                                            Wrap = WrapMode.NoWrap,
                                            Cells = [
                                                Text("@[Account]", colSpan: 3),
                                                Text("@[TrialBalance.Start]", colSpan: 2),
                                                Text("@[TrialBalance.Turnover]", colSpan: 2),
                                                Text("@[TrialBalance.End]", colSpan: 2)
                                            ]
                                        },
                                        new SheetRow() {
                                            Style = RowStyle.Header,
                                            Cells = [
                                                Text("@[Code]"), Text("@[Name]", colSpan: 2),
                                                Text("@[Debit]"), Text("@[Credit]"),
                                                Text("@[Debit]"), Text("@[Credit]"),
                                                Text("@[Debit]"), Text("@[Credit]")
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
                                                ..pairs.Select(p => Sum($"RepData.{p}"))
                                            ]
                                        }
                                    ]
                                },
                                new SheetSection() {
                                    Bindings = b => b.SetBinding(nameof(SheetSection.ItemsSource), new Bind("RepData.Items")),
                                    Children = [
                                        new SheetRow() {
                                            Align = TextAlign.Right,
                                            Wrap = WrapMode.NoWrap,
                                            Cells = [
                                                new SheetCell() {
                                                    Align = TextAlign.Center,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Id"))
                                                },
                                                new SheetCell() {
                                                    ColSpan = 2,
                                                    Align = TextAlign.Left,
                                                    Wrap = WrapMode.Wrap,
                                                    MinWidth = Length.FromString("20rem"),
                                                    Bindings = b => {
                                                        b.SetBinding(nameof(SheetCell.Content), new Bind("Name"));
                                                        b.SetBinding(nameof(SheetCell.Tip), new Bind("Name"));
                                                    }
                                                },
                                                ..pairs.Select(p => Sum(p))
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

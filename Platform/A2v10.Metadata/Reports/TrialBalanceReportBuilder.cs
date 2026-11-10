// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;


namespace A2v10.Metadata;

/* The trial balance of a ledger: per account, the opening balance, the debit and credit turnover of
 * the period, the closing balance - each balance as a debit/credit pair.
 *
 * A balance is laid out by the account's NormalBalance, not by its sign: Debit keeps it in the debit
 * column even when negative (a minus on 281 is a visible error, moved to credit it would be a
 * plausible number), Credit mirrors that, Both goes by sign. It is laid out per account and only then
 * summed - the signed total of a balanced ledger is zero, so the total row is the sum of the pairs.
 *
 * Split goes by sign as Both does, one grain finer: per account and object of its SplitBy, then summed
 * into the account. The grain is all it changes - for Debit and Credit a finer grain sums to the same
 * number, only a layout by sign is not additive.
 *
 * An off-balance account keeps its row and stays out of the total: debit against credit in the total
 * is what shows an opening balance that does not add up, and such an account would blur it.
 */
internal class TrialBalanceReportBuilder(IServiceProvider serviceProvider, ReportMetadata report, TableMetadata source, AppPlatformId platformId)
    : LedgerReportBuilder(serviceProvider, report, source, platformId)
{
    internal override String Reading => """
        One row per account (Acc). DtStart, CtStart - the balance before the period; DtSum, CtSum - the debit and the credit turnover within it; DtEnd, CtEnd - the balance at its end.
        A balance lies on the side the account keeps it: a minus in a debit column is an error in the books, not a credit balance.
        The balance on a date: Period with To alone, read DtEnd and CtEnd.
        OffBalance true: the account is outside the balance, and its row is not in the summary.
        A filter on an analytic keeps only the legs that carry it, so debit and credit do not meet in the summary: that says nothing about the books.
        With no such filter (a filter by company keeps both legs), debit differing from credit means the postings do not add up: most often the opening balance is not fully entered.
        """;

    internal override IEnumerable<String> RowNumbers => ["DtStart", "CtStart", "DtSum", "CtSum", "DtEnd", "CtEnd"];

    internal override IEnumerable<String> RowDetails => [OffBalance];

    private const String OffBalance = nameof(AccountType.OffBalance);

    // the pair a balance is laid out into, by the side the account keeps it on
    private static String Debit(String balance) => $"""
        case a.[{Constants.FieldNames.NormalBalance}] when N'{nameof(NormalBalance.Debit)}' then {balance}
            when N'{nameof(NormalBalance.Credit)}' then 0 else iif({balance} > 0, {balance}, 0) end
        """;
    private static String Credit(String balance) => $"""
        case a.[{Constants.FieldNames.NormalBalance}] when N'{nameof(NormalBalance.Debit)}' then 0
            when N'{nameof(NormalBalance.Credit)}' then -{balance} else iif({balance} < 0, -{balance}, 0) end
        """;

    /* The grain of J, beyond the account: a column per name some account is split by, holding the
     * leg's value on those accounts and null on the rest - so an account not split by it keeps the
     * account as its grain. The accounts come from the chart's file (SeedRow.SplitBy), not the table:
     * the column list shapes the query, and a query is not composed from data.
     */
    internal (String Apply, String GroupBy) SplitGrain()
    {
        var acc = Constants.FieldNames.Acc;
        var split = _acc.RefTableCheck.Storage.Rows
            .SelectMany(r => r.SplitBy.Select(c => (Column: c, Account: r.Id)))
            .GroupBy(x => x.Column, x => x.Account)
            .ToList();
        if (split.Count == 0)
            return (String.Empty, String.Empty);
        var values = split.Select(g => $"case when j.[{acc}] in ({String.Join(", ", g.Select(_acc.SqlLiteral))}) then j.[{g.Key}] end");
        var names = split.Select(g => $"[{g.Key}]");
        return ($"""

                cross apply (values ({String.Join(", ", values)})) k({String.Join(", ", names)})
            """, String.Concat(names.Select(n => $", k.{n}")));
    }

    protected override String CreateBodySql()
    {
        var acc = Constants.FieldNames.Acc;
        var sum = Constants.FieldNames.Sum;
        var inOut = Constants.FieldNames.InOut;
        var (apply, groupBy) = SplitGrain();

        // the total row of the rollup leaves the off-balance accounts out, an account's row is all of itself
        String InTotal(String number) =>
            $"[{number}] = iif(grouping([{acc}]) = 1, sum(iif([{OffBalance}] = 1, 0, [{number}])), sum([{number}]))";

        return $"""
        with J as (
            select j.[{acc}],
                [Start] = sum(case when j.[Date] < @From then j.[{sum}] * j.[{inOut}] else 0 end),
                [DtSum] = sum(case when j.[Date] >= @From and j.[{inOut}] = 1 then j.[{sum}] else 0 end),
                [CtSum] = sum(case when j.[Date] >= @From and j.[{inOut}] = -1 then j.[{sum}] else 0 end),
                [End] = sum(j.[{sum}] * j.[{inOut}])
            from {_source.SqlTableName} j{apply}
            where @Run = 1 and j.[Date] < @end{_grouping.SqlWhereClause("j")}
            group by j.[{acc}]{groupBy}
        ),
        S as (
            select J.[{acc}], J.[DtSum], J.[CtSum],
                [{OffBalance}] = iif(a.[{Constants.FieldNames.AccountType}] = N'{OffBalance}', 1, 0),
                [DtStart] = {Debit("J.[Start]")},
                [CtStart] = {Credit("J.[Start]")},
                [DtEnd] = {Debit("J.[End]")},
                [CtEnd] = {Credit("J.[End]")}
            from J inner join {ChartTableName} a on a.[Id] = J.[{acc}]
        ),
        T as (
            select [{acc}], [Grp] = grouping([{acc}]), [{OffBalance}] = max([{OffBalance}]),
                {String.Join(",\n        ", RowNumbers.Select(InTotal))}
            from S
            group by rollup([{acc}])
        )
        select [RepData!TRepData!Group] = null, [Id!!Id] = T.[{acc}],
            [Name] = a.[Name], [{OffBalance}] = cast(T.[{OffBalance}] as bit),
            T.[DtStart], T.[CtStart], T.[DtSum], T.[CtSum], T.[DtEnd], T.[CtEnd],
            [{acc}!!GroupMarker] = T.[Grp],
            [Items!TRepData!Items] = null
        from T left join {ChartTableName} a on a.[Id] = T.[{acc}]
        where @Run = 1
        order by T.[Grp] desc, T.[{acc}];
        """;
    }

    // one list of accounts, two blocks of the page: the model reads the same rows, marked
    protected override String TemplateProperties =>
        $"'TRepDataArray.$Balance': balanceRows, 'TRepDataArray.${OffBalance}': offBalanceRows";

    protected override String TemplateFunctions => $$"""
        function balanceRows() {
            return this.filter(r => !r.{{OffBalance}});
        }
        function offBalanceRows() {
            return this.filter(r => r.{{OffBalance}});
        }
        """;
}

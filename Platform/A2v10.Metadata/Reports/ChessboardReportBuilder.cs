// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;


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
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;
using System.Linq;
using System.Text;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* A fixed report over a ledger - trialBalance, chessboard. The account is the grouping and the sum is
 * the measure, both the ledger's baseline, so nothing is picked but the period and the filters: a
 * subset of either would not add up. What differs is the body of the query and the page; the
 * parameters, the period and the echo of the filters are here.
 */
internal abstract class LedgerReportBuilder : BaseReportBuilder
{
    protected readonly TableColumn _acc;

    protected LedgerReportBuilder(IServiceProvider serviceProvider, ReportMetadata report, TableMetadata source, AppPlatformId platformId)
        : base(serviceProvider, report, source, platformId)
    {
        if (source.Kind != EndpointKind.Ledger)
            throw new InvalidOperationException(
                $"Report '{report.Type}': surface {source.Path} is a {source.Kind}; this report reads a ledger");
        _acc = source.AllColumns().First(c => c.Name == Constants.FieldNames.Acc);
    }

    protected override Boolean HasChoices => false;

    // the chart the accounts are named from
    protected String ChartTableName => _acc.RefTableCheck.Storage.SqlTableName;

    // the statements between the period and the echo of the filters; @From, @end and @Run are declared
    protected abstract String CreateBodySql();

    public override async Task<IDataModel> LoadReportModelAsync(IModelView view, ExpandoObject prms)
    {
        SetGrouping(prms);
        return await _dbContext.LoadModelSqlAsync(view.DataSource, CreateSqlText(), dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id);
            dbprms
             .AddDateFromQuery("@From", prms, "From")
             .AddDateFromQuery("@To", prms, "To")
             .AddBitFromQuery("@Run", prms, "Run")
             .AddStringFromQuery("@Tab", prms, "Tab");
            foreach (var r in _grouping.Filters)
            {
                if (r.DataType == ColumnType.Operation)
                    dbprms.AddStringFromQuery($"@{r.Column}", prms, r.Column);
                else
                    dbprms.AddBigIntFromQuery($"@{r.Column}", prms, r.Column);
            }
        });
    }

    private String CreateSqlText()
    {
        var filterFields = _grouping.Filters.Select(f => $"[{f.Column}!T{f.Column}!RefId] = @{f.Column}");
        var filterSql = filterFields.Any() ? $"{String.Join(", ", filterFields)}, " : String.Empty;

        var filterMaps = new StringBuilder();
        foreach (var f in _grouping.Filters)
            filterMaps.AppendLine($"""
                select [!T{f.Column}!Map] = null, [Id!!Id] = [Id], [Name!!Name] = [Name]
                from {f.SqlTableName}
                where [Id] = @{f.Column};
                """);

        return $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        declare @now date = getdate();
        set @From = isnull(@From, datefromparts(year(@now), month(@now), 1));
        set @To = isnull(@To, eomonth(@From));
        declare @end date = dateadd(day, 1, @To);

        {CreateBodySql()}

        select [Filter!TFilter!Object] = null, {filterSql}
            [Period.From!TPeriod!] = @From, [Period.To!TPeriod!] = @To, Run = @Run, Tab = @Tab;

        {filterMaps}
        """;
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

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
        if (source.Kind != TableKind.Ledger)
            throw new InvalidOperationException(
                $"Report '{report.Type}': surface {source.Path} is a {source.Kind}; this report reads a ledger");
        _acc = source.AllColumns().First(c => c.Name == Constants.FieldNames.Acc);
    }

    protected override Boolean HasChoices => false;

    internal override IEnumerable<String> Measures => [Constants.FieldNames.Sum];

    internal override IReadOnlyList<String> RowGroups => [Constants.FieldNames.Acc];

    // the account is the row itself here, where a turnover's group is an object on it
    internal override ExpandoObject? RowGroup(ExpandoObject node, String group)
    {
        var account = new ExpandoObject();
        account.Set(Constants.FieldNames.Id, node.Get<Object>(Constants.FieldNames.Id));
        account.Set(Constants.FieldNames.Name, node.Get<String>(Constants.FieldNames.Name));
        return account;
    }

    // the chart the accounts are named from
    protected String ChartTableName => _acc.RefTableCheck.Storage.SqlTableName;

    // the statements between the period and the echo of the filters; @From, @end and @Run are declared
    protected abstract String CreateBodySql();

    public override async Task<IDataModel> LoadReportModelAsync(String? dataSource, ExpandoObject prms)
    {
        SetGrouping(prms);
        return await _dbContext.LoadModelSqlAsync(dataSource, CreateSqlText(), dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id);
            dbprms
             .AddDateFromQuery("@From", prms, "From")
             .AddDateFromQuery("@To", prms, "To")
             .AddBitFromQuery("@Run", prms, "Run")
             .AddStringFromQuery("@Tab", prms, "Tab");
            AddFilterParameters(dbprms, prms);
        });
    }

    internal String CreateSqlText()
    {
        var filterFields = _grouping.Filters.Select(f => $"[{f.Column}!T{f.Column}!RefId] = {SqlExtensions.ColumnParam(f.Column)}");
        var filterSql = filterFields.Any() ? $"{String.Join(", ", filterFields)}, " : String.Empty;

        return $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        {GateSql()}

        declare @now date = getdate();
        set @From = isnull(@From, datefromparts(year(@now), month(@now), 1));
        set @To = isnull(@To, eomonth(@From));
        declare @end date = dateadd(day, 1, @To);
        {FilterStartSql()}

        {CreateBodySql()}

        select [Filter!TFilter!Object] = null, {filterSql}
            [Period.From!TPeriod!] = @From, [Period.To!TPeriod!] = @To, Run = @Run, Tab = @Tab;

        {_grouping.FilterMapsSql()}
        """;
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;
using System.Linq;
using System.Collections.Generic;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal class TurnoverReportBuilder(IServiceProvider serviceProvider, ReportMetadata report, TableMetadata source, AppPlatformId platformId) 
    : BaseReportBuilder(serviceProvider, report, source, platformId)
{
    public override async Task<IDataModel> LoadReportModelAsync(IModelView view, ExpandoObject prms)
    {
        SetGrouping(prms);
        var sqlString = await CreateSqlTextAsync();

        return await _dbContext.LoadModelSqlAsync(view.DataSource, sqlString, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id);
            dbprms
             .AddDateFromQuery("@From",  prms, "From")
             .AddDateFromQuery("@To", prms, "To")
             .AddBitFromQuery("@Run", prms, "Run")
             .AddStringFromQuery("@Tab", prms, "Tab");
            AddFilterParameters(dbprms, prms);
        });
    }

    internal async Task<String> CreateSqlTextAsync()
    {
        var filterFields = _grouping.Filters.Select(f =>
            $"[{f.Column}!T{f.Column}!RefId] = {SqlExtensions.ColumnParam(f.Column)}");

        var filterSql = filterFields.Any()
            ? $"{String.Join(", ", filterFields)}, "
            : String.Empty;

        IEnumerable<String> createTempTableFeilds()
        {
            foreach (var f in _grouping.Filters.Union(_grouping.Grouping).Distinct(Comparers.ReportItemMetadata))
                yield return CreateField(f);
            foreach (var f in _grouping.Data)
            {
                yield return CreateField(f, "Start");
                yield return CreateField(f, "In");
                yield return CreateField(f, "Out");
            }
        }

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        {GateSql()}

        drop table if exists #tmpturn;
        create table #tmpturn({String.Join(',', createTempTableFeilds())});
                
        declare @now date = getdate();
        set @From = isnull(@From, datefromparts(year(@now), month(@now), 1));
        set @To = isnull(@To, eomonth(@From));
        declare @end date = dateadd(day, 1, @To);    

        insert into #tmpturn({_grouping.SimpleFields()}, {_grouping.SimpleDataFields()})
        select {_grouping.SelectFields("j")},
            {_grouping.InsertIntoDataFields("j")}
        from {_source.SqlTableName} j
        where @Run = 1 and [Date] < @end
            {_grouping.SqlWhereClause("j")} 
        group by {_grouping.SelectFields("j")};

        -- Main Select
        with T as (
            select 
                {_grouping.FieldsWithGrouping()},
                {_grouping.AggregateDataFields()}
            from #tmpturn
            where @Run = 1
            group by rollup({_grouping.SimpleFields()})
        )
        select [RepData!TRepData!Group] = null, 
            {_grouping.ReferenceWithGrouping()},
            {_grouping.FullDataFields()}, 
            [Items!TRepData!Items] = null
        from T
            {_grouping.ReferenceJoins()}
        where @Run = 1
        order by {_grouping.ReferenceOrderByGrp()};

        select [Filter!TFilter!Object] = null, {filterSql}
            [Group] = N'{_grouping.GroupColumns()}',
            [Period.From!TPeriod!] = @From, [Period.To!TPeriod!] = @To, Run = @Run, Tab = @Tab;

        {_grouping.FilterMapsSql()}

        {_grouping.RepInfoSql()}
        """;

        return sqlString;
    }
}

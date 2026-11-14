// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data.Common;
using System.Dynamic;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal abstract class BaseReportBuilder(IServiceProvider serviceProvider, ReportMetadata report, TableMetadata source, AppPlatformId platformId)
{
    protected IServiceProvider _serviceProvider = serviceProvider;
#pragma warning disable IDE1006 // Naming Styles
    protected TableMetadata _source => source;
    protected ReportMetadata _report => report;
    protected IDbContext _dbContext => _serviceProvider.GetRequiredService<IDbContext>();
    protected ICurrentUser _currentUser => _serviceProvider.GetRequiredService<ICurrentUser>();

    protected ReportGrouping _grouping = default!;
#pragma warning restore IDE1006 // Naming Styles

    // the report's own address: the right is on it, not on the surface it reads
    internal required String Path { get; init; }
    internal required Boolean UseGrants { get; init; }

    // a report only reads
    protected String GateSql() => UseGrants ? Gate.View.Sql(Path) : String.Empty;

    internal String CreateField(ReportItemMetadata item, String? prefix = null)
    {
        // TODO : PlatformId DataType

        var dt = item.DataType.ToSqlDataType();
        if (dt == "platformid")
            dt = platformId.SqlTypeName;
        return $"[{prefix}{item.Column}] {dt}";
    }

    // a filter is a reference: typed as its key, empty is 'not picked' - the WHERE reads null as no filter
    protected void AddFilterParameters(DbParameterCollection dbprms, ExpandoObject prms)
    {
        foreach (var f in _grouping.Filters)
            dbprms.AddReference(SqlExtensions.ColumnParam(f.Column), f.DataType, platformId,
                prms.Get<Object>(f.Column)?.ToString());
    }

    /* What the filters start on, and what a run cannot go without. A start is for the page that opens
     * (no Run): a run is exactly its url, so a filter the user cleared stays cleared, and report_query,
     * always a run, gets no preset. The refusal is the batch's, so the page ($alertUi) and the model
     * read the one judge.
     */
    protected String FilterStartSql()
    {
        var initials = _report.InitialValues
            .Select(x => (Name: x.Key, Initial: x.Value, Column: _source.AllColumns().First(c => c.Name == x.Key)))
            .ToList();
        // a variable for what is computed first, as a record's (SqlInitialValue): an expression, a profile slot
        IEnumerable<String> declares = initials.Where(x => x.Initial.Source is InitialSource.Sql or InitialSource.Profile)
            .Select(x => x.Initial.Source == InitialSource.Sql
                ? $"declare @Init{x.Name} {x.Column.SqlDataType()} = ({x.Initial.Value});"
                : $"declare @Init{x.Name} {x.Column.SqlDataType()};");
        var slots = initials.Where(x => x.Initial.Source == InitialSource.Profile)
            .Select(x => ($"@Init{x.Name}", x.Initial.Value)).ToList();
        if (slots.Count > 0)
            declares = declares.Append(SqlExtensions.ProfileReadSql(slots));
        var sets = initials.Select(x =>
            $"set {SqlExtensions.ColumnParam(x.Name)} = isnull({SqlExtensions.ColumnParam(x.Name)}, {x.Column.SqlInitialValue(x.Initial)});");
        var start = initials.Count == 0 ? String.Empty : $"""
            if isnull(@Run, 0) = 0
            begin
            {String.Join("\n", declares.Concat(sets))}
            end;
            """;
        var refuse = _report.Required.Count == 0 ? String.Empty : $"""
            if @Run = 1 and ({String.Join(" or ", _report.Required.Select(r => $"{SqlExtensions.ColumnParam(r)} is null"))})
                {SqlExtensions.UserError("Report.Error.FilterRequired")}
            """;
        return $"{start}\n{refuse}";
    }

    /* What a report TYPE means to the query and the template. The runtime asks on its way to the data
     * and the validator on its way to the screen; the page half is ReportPage.Create, and the
     * validator builds both for every report, so a fourth type taught to one of the two fails there.
     */
    internal static BaseReportBuilder Create(IServiceProvider serviceProvider,
        ReportEndpointMetadata endpoint, AppPlatformId platformId, Boolean useGrants) => endpoint.Report.Type switch
    {
        "turnover" => new TurnoverReportBuilder(serviceProvider, endpoint.Report, endpoint.Surface, platformId) { Path = endpoint.Path, UseGrants = useGrants },
        "trialBalance" => new TrialBalanceReportBuilder(serviceProvider, endpoint.Report, endpoint.Surface, platformId) { Path = endpoint.Path, UseGrants = useGrants },
        "chessboard" => new ChessboardReportBuilder(serviceProvider, endpoint.Report, endpoint.Surface, platformId) { Path = endpoint.Path, UseGrants = useGrants },
        _ => throw new NotImplementedException($"Invalid Report type '{endpoint.Report.Type}' for {endpoint.Path}")
    };

    /* The picks the query is built for - which groups, which data - resolved against the surface.
     * The page resolves its own from the same url (ReportPage.Create).
     */
    internal void SetGrouping(ExpandoObject prms) =>
        _grouping = new ReportGrouping(_report, _source, prms);

    public abstract Task<IDataModel> LoadReportModelAsync(String? dataSource, ExpandoObject prms);

    // what the user picks besides the period and the filters: groups and data; a fixed report has none
    protected virtual Boolean HasChoices => true;

    /* What a type means to the model, the third half beside the query and the page. Here and not in the tools
     * so that they never ask the type; abstract, so a fourth type does not build without it.
     */
    // what a row holds and how to read it, in the words of the row's own names
    internal abstract String Reading { get; }
    internal virtual IEnumerable<String> Measures => _report.Data;
    internal abstract IEnumerable<String> RowNumbers { get; }
    // what a row carries besides its numbers: nothing to sort or sum by
    internal virtual IEnumerable<String> RowDetails => [];
    // what a row is per, after the load: the depth of the tree, each level one name
    internal virtual IReadOnlyList<String> RowGroups => [.. _grouping.Grouping.Select(c => c.Column)];
    internal virtual ExpandoObject? RowGroup(ExpandoObject node, String group) => node.Get<ExpandoObject>(group);

    // what a page computes on the client: template properties, and the functions they name
    protected virtual String TemplateProperties => String.Empty;
    protected virtual String TemplateFunctions => String.Empty;

    public virtual String CreateTemplate()
    {
        var choices = HasChoices ? """
            let group = this.GroupingInfo.filter(f => f.Checked).map(f => f.Id).join('!');
            let datinfo = this.DataInfo.filter(f => f.Checked).map(f => f.Id).join('!');

            if (!group) {
                ctrl.$alert(`@[Report.Error.AtLeastGroup]`);
                return;
            }

            if (!datinfo) {
                ctrl.$alert(`@[Report.Error.AtLeastData]`);
                return;
            }

            let filter = {
                Run : true,
                Group: group,
                Data: datinfo,
                Tab : repFilter.Tab
            };
            """ : """
            let filter = {
                Run : true,
                Tab : repFilter.Tab
            };
            """;

        return $$"""
        const utils = require("std:utils");
        const template = {
            options: {
                noDirty: true
            },
            properties: {
                'TRoot.$$Dirty': Boolean,
                'TRoot.$$Loading': Boolean,
                'TRoot.$AlertVisible'() { return this.$$Dirty && !this.$$Loading && this.Filter.Run; },
                'TRoot.$SheetPageClass'() { return this.$AlertVisible ? 'sheet-dirty' : undefined },
                {{TemplateProperties}}
            },
            commands: {
                generate
            },
            events: {
                'Model.dirty.change': modelChanged
            }
        };

        module.exports = template;

        function modelChanged(dirty, prop) {
        	if (!dirty) return;
        	if (prop === 'Filter.Tab')
        		return;
        	this.$$Dirty = true;
        }
        
        function generate() {
            const ctrl = this.$ctrl;
            
            let repFilter = this.Filter;

        {{choices}}
            this.$$Loading = true;
            let defNames = ['Run', 'Tab'];

            for (let fld of Object.getOwnPropertyNames(repFilter)
                .filter(f => !f.startsWith('_') && !f.startsWith('$') && !defNames.includes(f))) {
                if (fld === 'Period')
                    filter[fld] = repFilter.Period.format('DateUrl');
                else if (utils.isObjectExact(repFilter[fld]))   
                    filter[fld] = repFilter[fld].Id;
            }
            ctrl.$requery(filter);
        }

        {{TemplateFunctions}}

        """;
    }

}

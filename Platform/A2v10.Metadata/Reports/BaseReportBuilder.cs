// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data.Common;
using System.Dynamic;

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

    /* A filter parameter is declared as the key of its column (ToSqlDbTypeInfo): a platformid in the
     * application's base, a code as a string. Bigint for everything but an operation sent an enum code
     * to an int and a guid to nothing. Empty is 'not picked' - the WHERE reads null as no filter.
     */
    protected void AddFilterParameters(DbParameterCollection dbprms, ExpandoObject prms)
    {
        foreach (var f in _grouping.Filters)
        {
            var value = prms.Get<Object>(f.Column)?.ToString();
            if (f.DataType.ToSqlDbTypeInfo().SqlName == "platformid")
                dbprms.AddTyped(SqlExtensions.ColumnParam(f.Column), platformId.SqlDbType, platformId.ParseId(value));
            else
                dbprms.AddString(SqlExtensions.ColumnParam(f.Column), String.IsNullOrEmpty(value) ? null : value);
        }
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

    public abstract Task<IDataModel> LoadReportModelAsync(IModelView view, ExpandoObject prms);

    // what the user picks besides the period and the filters: groups and data; a fixed report has none
    protected virtual Boolean HasChoices => true;

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

// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Dynamic;
using System.Globalization;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class SqlBuilder(BuilderDescriptor desciptor, IServiceProvider serviceProvider)
{
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();
    private readonly ICurrentUser _currentUser = serviceProvider.GetRequiredService<ICurrentUser>();
    private readonly DatabaseMetadataProvider _metadataProvider = serviceProvider.GetRequiredService<DatabaseMetadataProvider>();
    private readonly BuilderDescriptor _descr = desciptor;
    private readonly NormalEndpointMetadata Endpoint = desciptor.Endpoint;
    private readonly TableMetadata Table = desciptor.Endpoint.Storage;
    private readonly String? DataSource = desciptor.DataSource;
    private readonly AppPlatformId PlatformId = desciptor.PlatformId;


    DbParameterCollection AddDefaultParameters(DbParameterCollection prms)
    {
        if (_currentUser.Identity.Tenant != null)
            prms.AddInt("@TenantId", _currentUser.Identity.Tenant);
        prms.AddBigInt("@UserId", _currentUser.Identity.Id);
        return prms;
    }

    DbParameterCollection AddPeriodParameters(DbParameterCollection prms, ExpandoObject? qry)
    {
        if (!Table.HasPeriod)
            return prms;

        static DateTime? DateTimeFromString(String? value)
        {
            if (value == null)
                return null;
            return DateTime.ParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture);
        }

        return prms.AddDate("@From", DateTimeFromString(qry?.Get<String>("From")))
            .AddDate("@To", DateTimeFromString(qry?.Get<String>("To")));
    }

    /* One entry per SET and not per column: two columns onto one set share a single array in the
     * model, exactly as they share one map. The details are walked only where the model carries
     * them - a row in a details table picks from the same list its header does.
     */
    IEnumerable<TableMetadata> EnumTargets(Boolean withDetails)
    {
        var columns = withDetails
            ? Table.AllColumns().Concat(Table.Details.Values.SelectMany(d => d.AllColumns()))
            : Table.AllColumns();
        return columns.Where(c => c.IsEnum)
            .Select(c => c.RefTableCheck.Storage)
            .DistinctBy(t => t.SqlTableName);
    }

    /* The candidates of an enum: the whole set, and therefore never the map. The map holds the
     * values the loaded rows happen to reference - it must, so that a record on a withdrawn code
     * still shows its name - which makes it both too short to choose from and too long to offer.
     * Ordered by the set's own Order, never by Name: Name is a resource key, so alphabetical there
     * means alphabetical by code.
     *
     * 'All' belongs to the filter alone. It is a row of the set like any other (its key is the
     * empty string), and it says 'do not restrict' - a statement about a query, which a record
     * cannot make about its own value.
     */
    String EnumValuesRecordset(TableMetadata target, Boolean withAll)
    {
        var exceptAll = withAll ? String.Empty : $" and e.[{Constants.FieldNames.Id}] <> N''";
        return $"""
        -- {target.Model} - values
        select [{target.CollectionName}!{target.TypeName}!Array] = null,
            [Id!!Id] = e.[Id], [Name!!Name] = e.[Name]
        from {target.SqlTableName} e where e.[{Constants.FieldNames.Void}] = 0{exceptAll}
        order by e.[{Constants.FieldNames.Order}];
        """;
    }
}

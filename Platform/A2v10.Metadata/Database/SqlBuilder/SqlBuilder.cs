// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data.Common;
using System.Dynamic;
using System.Globalization;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class SqlBuilder(BuilderDescriptor desciptor, IServiceProvider serviceProvider)
{
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();
    private readonly ICurrentUser _currentUser = serviceProvider.GetRequiredService<ICurrentUser>();
    private readonly BuilderDescriptor _descr = desciptor;
    private readonly TableMetadata Table = desciptor.Table;
    private readonly String? DataSource = desciptor.DataSource;


    DbParameterCollection AddDefaultParameters(DbParameterCollection prms)
    {
        if (_currentUser.Identity.Tenant != null)
            prms.AddInt("@TenantId", _currentUser.Identity.Tenant);
        prms.AddBigInt("@UserId", _currentUser.Identity.Id);
        return prms;
    }

    DbParameterCollection AddPeriodParameters(DbParameterCollection prms, ExpandoObject? qry)
    {
        if (!Table.HasPeriod())
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
}

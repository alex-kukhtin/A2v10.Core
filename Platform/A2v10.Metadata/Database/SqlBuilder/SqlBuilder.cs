// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Dynamic;
using System.Globalization;
using System.Linq;

namespace A2v10.Metadata;

internal partial class SqlBuilder(BuilderDescriptor desciptor, IServiceProvider serviceProvider)
{
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();
    private readonly ICurrentUser _currentUser = serviceProvider.GetRequiredService<ICurrentUser>();
    private readonly BuilderDescriptor _descr = desciptor;
    private readonly IEnumerable<ReferenceMember> RefFields = desciptor.RefFields;
    private readonly TableMetadata Table = desciptor.Table;
    private readonly String? DataSource = desciptor.DataSource;

    String RefFieldsEnum(Func<ReferenceMember, String> func)
    {
        // with tail comma!
        if (!RefFields.Any())
            return String.Empty;
        return String.Join(", ", RefFields.Select(r => func(r))) + ", ";
    }

    String RefFieldsMap()
    {
        var maps = RefFields.GroupBy(r => r.Table.Path).Select(g =>
        {
            var r = g.First();
            return $"""
            with TX as (select [{r.Column.Name}] from @map where [{r.Column.Name}] is not null group by [{r.Column.Name}])
            select [!{r.Table.TypeName}!Map] = null, [Id!!Id] = m.[Id], [Name!!Name] = m.[Name] 
            from {r.Table.SqlTableName} m inner join TX on m.[Id] = TX.[{r.Column.Name}]
            """;
        });

        if (!maps.Any())
            return String.Empty;
        return $"""
            -- maps
            {String.Join("\n\n", maps) };
        """;
    }

    DbParameterCollection AddDefaultParameters(DbParameterCollection prms)
    {
        if (_currentUser.Identity.Tenant != null)
            prms.AddInt("@TenantId", _currentUser.Identity.Tenant);
        prms.AddBigInt("@UserId", _currentUser.Identity.Id);
        return prms;
    }

    DbParameterCollection AddPeriodParameters(DbParameterCollection prms, ExpandoObject? qry)
    {
        if (!_descr.Table.HasPeriod())
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

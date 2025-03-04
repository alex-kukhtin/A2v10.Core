﻿// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.Metadata.SqlServer;

internal partial class DatabaseModelProcessor(DatabaseMetadataProvider _metadataProvider, ICurrentUser _currentUser, IDbContext _dbContext)
{
    public async Task<(IDataModel, TableMetadata)> LoadModelAsync(IModelView view, IPlatformUrl platformUrl)
    {
        if (view.Meta == null)
           throw new InvalidOperationException("Meta is null");
        var meta = await _metadataProvider.GetSchemaAsync(view.Meta, view.DataSource);
        var dm = view.IsIndex ? await LoadIndexModelAsync(meta, platformUrl, view) : await LoadPlainModelAsync(meta, platformUrl, view);
        return (dm, meta);
    }

    private void AddDefaultParameters(DbParameterCollection prms)
    {
        if (_currentUser.Identity.Tenant != null)
            prms.AddInt("@TenantId", _currentUser.Identity.Tenant);
        prms.AddBigInt("@UserId", _currentUser.Identity.Id);
    }

    String RefTableJoins(List<(TableColumn Column, Int32 Index)> refFields)
    {
        var sb = new StringBuilder();
        foreach (var col in refFields)
        {
            var rc = col.Column.Reference!;
            sb.AppendLine($"""
                   left join {rc.RefSchema}.[{rc.RefTable}] r{col.Index} on a.[{col.Column.Name}] = r{col.Index}.[Id]
                 """);
        }
        return sb.ToString();
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata.SqlServer;

internal partial class DatabaseModelProcessor(DatabaseMetadataProvider _metadataProvider, ICurrentUser _currentUser, IDbContext _dbContext)
{
    public async Task<(IDataModel, TableMetadata)> LoadModelAsync(IModelView view, IPlatformUrl platformUrl)
    {
        if (view.Meta == null)
           throw new InvalidOperationException("Meta is null");
        var meta = await _metadataProvider.GetSchemaAsync(view.Meta, view.DataSource);
        var dm = view.IsIndex ? await LoadIndexModelAsync(meta, platformUrl, view) : await LoadPlainModelAsync(meta);
        return (dm, meta);
    }

    private void AddDefaultParameters(DbParameterCollection prms)
    {
        if (_currentUser.Identity.Tenant != null)
            prms.AddInt("@TenantId", _currentUser.Identity.Tenant);
        prms.AddBigInt("@UserId", _currentUser.Identity.Id);
    }
}

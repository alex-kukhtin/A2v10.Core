// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class DatabaseModelProcessor(DatabaseMetadataProvider _metadataProvider, ICurrentUser _currentUser, IDbContext _dbContext)
{
    public async Task<(IDataModel, TableMetadata)> LoadModelAsync(IModelView view, IPlatformUrl platformUrl)
    {
        if (view.Meta == null)
           throw new InvalidOperationException("Meta is null");
        var meta = await _metadataProvider.GetSchemaAsync(view.Meta, view.DataSource);

        AppMetadata appMeta = await _metadataProvider.GetAppMetadataAsync(view.DataSource);

        foreach (var c in meta.Columns.Where(c => c.IsReference))
        {
            var refTable = c.Reference;
            refTable.EndpointPath = _metadataProvider.GetOrAddEndpointPath(view.DataSource, refTable.RefSchema, refTable.RefTable);
        }

        var dm = view.IsIndex ? await LoadIndexModelAsync(meta, platformUrl, view, appMeta) : await LoadPlainModelAsync(meta, platformUrl, view, appMeta);
        return (dm, meta);
    }

    private void AddDefaultParameters(DbParameterCollection prms)
    {
        if (_currentUser.Identity.Tenant != null)
            prms.AddInt("@TenantId", _currentUser.Identity.Tenant);
        prms.AddBigInt("@UserId", _currentUser.Identity.Id);
    }

    String RefTableJoins(IEnumerable<(TableColumn Column, Int32 Index)> refFields, AppMetadata appMeta)
    {
        var sb = new StringBuilder();
        foreach (var col in refFields)
        {
            var rc = col.Column.Reference ??
                throw new InvalidOperationException("Invalid Reference");
            sb.AppendLine($"""
                left join {rc.RefSchema}.[{rc.RefTable}] r{col.Index} on a.[{col.Column.Name}] = r{col.Index}.[{appMeta.IdField}]
            """);
        }
        return sb.ToString();
    }
}

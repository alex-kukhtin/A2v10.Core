// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Text;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

public class DeployDatabaseHandler(IServiceProvider _serviceProvider) : IClrInvokeTarget
{
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    private readonly DatabaseMetadataCache _metadataCache = _serviceProvider.GetRequiredService<DatabaseMetadataCache>();

    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        var prms = new ExpandoObject()
        {
            {"UserId", args.Get<Object>("UserId") }
        };

        var dm = await _dbContext.LoadModelAsync(null, "a2meta.[Config.Load]", prms);
        var meta = AppMetadata.FromDataModel(dm);

        var dbCreator = new DatabaseCreator(meta);

        var sql = new StringBuilder("""
        set nocount on;
        set transaction isolation level read uncommitted;

        """);
        foreach (var t in meta.Tables)
        {
            var createTable = dbCreator.CreateTable(t);
            sql.AppendLine(createTable);
        }
        foreach (var t in meta.Tables)
        {
            var createTableType = dbCreator.CreateTableType(t);
            sql.AppendLine(createTableType);
        }
        foreach (var t in meta.Tables)
        {
            var createFK = dbCreator.CreateForeignKeys(t);
            sql.AppendLine(createFK);
        }

        await _dbContext.LoadModelSqlAsync(null, sql.ToString());

        _metadataCache.ClearAll();

        return new ExpandoObject();
    }
}

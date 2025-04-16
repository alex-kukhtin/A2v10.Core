// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

public class DeployDatabaseHandler(IServiceProvider _serviceProvider) : IClrInvokeTarget
{
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    private readonly DatabaseMetadataCache _metadataCache = _serviceProvider.GetRequiredService<DatabaseMetadataCache>();
    private readonly IBackgroundProcessHandler _backgroundProcessor = _serviceProvider.GetRequiredService<IBackgroundProcessHandler>();
    private readonly ISignalSender _signalSender = _serviceProvider.GetRequiredService<ISignalSender>();

    public Task<Object> InvokeAsync(ExpandoObject args)
    {
        var userIdObj = args.Get<Object>("UserId");
        if (userIdObj == null)
            throw new InvalidOperationException("UserId is null");
        var userId = Convert.ToInt64(userIdObj);
        if (userId == 0)
            throw new InvalidOperationException("UserId = 0");

        _backgroundProcessor.Execute(_serviceProvider => ProcessDataAsync(userId));

        return Task.FromResult<Object>(new ExpandoObject());
    }

    private async Task ProcessDataAsync(Int64 userId)
    {
        var prms = new ExpandoObject()
        {
            {"UserId", userId }
        };

        var dm = await _dbContext.LoadModelAsync(null, "a2meta.[Config.Load]", prms);
        var meta = AppMetadata.FromDataModel(dm);

        var dbCreator = new DatabaseCreator(meta);

        Task SendSignalAsync(String kind, TableMetadata table, Int32 index)
        {
            return _signalSender.SendAsync(
                new SignalResult(userId, "meta.deploy")
                {
                    Data = new ExpandoObject()
                    {
                        { "Kind", kind },
                        { "Schema", table.Schema},
                        { "Table", table.Name },
                        { "Index", index },
                    }
                }
            );
        }

        Int32 index = 0;
        foreach (var t in meta.Tables)
        {
            var createTable = dbCreator.CreateTable(t);
            await _dbContext.LoadModelSqlAsync(null, createTable);
            await SendSignalAsync("Table", t, index++);
        }
        index = 0;
        foreach (var t in meta.Tables)
        {
            var createTableType = dbCreator.CreateTableType(t);
            await _dbContext.LoadModelSqlAsync(null, createTableType);
            await SendSignalAsync("TableType", t, index++);
        }

        // Run before foreign keys, it may be used for operations.
        var sqlOps = dbCreator.CreateOperations(meta.Operations);
        if (!String.IsNullOrEmpty(sqlOps))
            await _dbContext.LoadModelSqlAsync(null, sqlOps);

        index = 0;
        foreach (var t in meta.Tables)
        {
            var createFK = dbCreator.CreateForeignKeys(t);
            if (!String.IsNullOrEmpty(createFK))
                await _dbContext.LoadModelSqlAsync(null, createFK);
            await SendSignalAsync("ForeignKey", t, index++);
        }

        _metadataCache.ClearAll();
    }
}

// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata;

public class DeployDatabaseHandler(IServiceProvider _serviceProvider) : IClrInvokeTarget
{
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    private readonly DatabaseMetadataCache _metadataCache = _serviceProvider.GetRequiredService<DatabaseMetadataCache>();
    private readonly IBackgroundProcessHandler _backgroundProcessor = _serviceProvider.GetRequiredService<IBackgroundProcessHandler>();
    private readonly ISignalSender _signalSender = _serviceProvider.GetRequiredService<ISignalSender>();

    public Task<Object> InvokeAsync(ExpandoObject args)
    {
        var userIdObj = args.Get<Object>("UserId") 
            ?? throw new InvalidOperationException("UserId is null");
        var userId = Convert.ToInt64(userIdObj);
        if (userId == 0)
            throw new InvalidOperationException("UserId = 0");

        _backgroundProcessor.Execute(_serviceProvider => ProcessDataAsync(userId));

        return Task.FromResult<Object>(new ExpandoObject());
    }

    private Task<IDataModel> UpdateMetadataAsync(AppMetadata appMeta)
    {
        var sql = """
            update a2meta.[Application] set Version = Version + 1 where Id = @Id;
            update a2sys.SysParams set [StringValue] = @Title where [Name] = N'AppTitle';
            if @@rowcount = 0
                insert into a2sys.SysParams ([Name], StringValue) values (N'AppTitle', @Title);
        """;
        return _dbContext.LoadModelSqlAsync(null, sql, new ExpandoObject()
        {
            { "Id", appMeta.Id },
            { "Title", appMeta.Title },
        });
    }

    private async Task ProcessDataAsync(Int64 userId)
    {
        var prms = new ExpandoObject()
        {
            {"UserId", userId }
        };

        var dm = await _dbContext.LoadModelAsync(null, "a2meta.[Config.Load]", prms);
        var meta = AppMetadata.FromDataModel(dm);

        await UpdateMetadataAsync(meta);

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

        foreach (var e in meta.Enums)
        {
            var createTable = DatabaseCreator.CreateEnum(e);
            if (!String.IsNullOrEmpty(createTable))
                await _dbContext.LoadModelSqlAsync(null, createTable);
            var enumTable = DatabaseCreator.CreateEnumTable(e);
            await _dbContext.LoadModelSqlAsync(null, DatabaseCreator.MergeEnums(e), dbprms =>
            {
                dbprms.AddStructured($"@Enums", "a2meta.[Enum.TableType]", enumTable);
            });
            // TODO: signal
        }

        Int32 index = 0;

        foreach (var t in meta.Tables)
        {
            var createTable = dbCreator.CreateTable(t);
            if (!String.IsNullOrEmpty(createTable))
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
        var sqlOps = DatabaseCreator.CreateOperations(meta.Operations);
        if (!String.IsNullOrEmpty(sqlOps))
        {
            // create
            await _dbContext.LoadModelSqlAsync(null, sqlOps);
            // merge
            await _dbContext.LoadModelSqlAsync(null, DatabaseCreator.MergeOperations(), dbprms =>
            {
                dbprms.AddStructured($"@Operations", "a2meta.[Operation.TableType]", DatabaseCreator.CreateOperationTable(meta.Operations));
            });
        }

        index = 0;
        foreach (var t in meta.Tables)
        {
            var createFK = dbCreator.CreateForeignKeys(t);
            if (!String.IsNullOrEmpty(createFK))
                await _dbContext.LoadModelSqlAsync(null, createFK);
            await SendSignalAsync("ForeignKey", t, index++);
        }

        _metadataCache.ClearAll();
        await _signalSender.SendAsync(new SignalResult(userId, "meta.deploy.complete"));
    }
}

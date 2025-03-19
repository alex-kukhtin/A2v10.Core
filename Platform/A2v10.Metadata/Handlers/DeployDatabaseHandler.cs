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

    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        var prms = new ExpandoObject()
        {
            {"UserId", args.Get<Object>("UserId") }
        };

        var dm = await _dbContext.LoadModelAsync(null, "a2meta.[Config.Load]", prms);
        var meta = AppMetadata.FromDataModel(dm);

        var dbCreator = new DatabaseCreator(meta);
        foreach (var t in meta.Tables)
        {
            var createTable = dbCreator.CreateTable(t);
            var tt = dbCreator.CreateTableType(t);
            int z = 55;
        }

        return new ExpandoObject();
    }
}

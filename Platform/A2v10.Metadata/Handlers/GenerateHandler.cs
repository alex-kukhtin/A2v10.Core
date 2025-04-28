// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.Metadata;

public class GenerateHandler(IServiceProvider _serviceProvider) : IClrInvokeTarget
{
    private readonly DatabaseMetadataProvider _metadataProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        var userIdObj = args.Get<Object>("UserId");
        var endpoint = args.Get<Object>("Id")
            ?? throw new InvalidOperationException("Argument 'Id' not found");

         var dm = await _dbContext.LoadModelSqlAsync(null, "a2meta.[Generate]", args);
        var schema = "";
        var table = "";
        await _metadataProvider.GetSchemaAsync(null, schema, table);
        var modelInfo = _metadataProvider.GetSchemaAsync(null, schema, table);
            ?? throw new InvalidOperationException($"ModelInfo not found for {endpoint}");

        throw new InvalidOperationException($"GENERATE FOR {endpoint}");
    }
}

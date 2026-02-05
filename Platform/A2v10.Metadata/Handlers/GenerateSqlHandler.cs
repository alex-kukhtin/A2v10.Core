// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

public class GenerateSqlHandler(IServiceProvider _serviceProvider) : IClrInvokeTarget
{
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    private readonly IAppCodeProvider _appCodeProvider = _serviceProvider.GetRequiredService<IAppCodeProvider>();
    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        var userIdObj = args.Get<Object>("UserId")
            ?? throw new InvalidOperationException("UserId is null");
        var userId = Convert.ToInt64(userIdObj);
        if (userId == 0)
            throw new InvalidOperationException("UserId = 0");

        var prms = new ExpandoObject()
        {
            {"UserId", userId }
        };

        var dm = await _dbContext.LoadModelAsync(null, "a2meta.[Config.Load]", prms);
        var meta = AppMetadata.FromDataModel(dm);

        var path = _appCodeProvider.GetMainModuleFullPath("_sql", String.Empty);

        var generator = new SqlScriptGenerator(meta, path);
        await generator.GenerateSqlScriptsAsync();

        return new ExpandoObject();
    }
}

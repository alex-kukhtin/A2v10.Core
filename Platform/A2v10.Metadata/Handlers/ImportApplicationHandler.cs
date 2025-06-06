// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Services;

namespace A2v10.Metadata;

public class ImportApplicationHandler(IServiceProvider _serviceProvider) : IClrInvokeTarget
{
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    private readonly ICurrentUser _currentUser = _serviceProvider.GetRequiredService<ICurrentUser>();
    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        var blobObj = args.Get<Object>("Blob");
        if (blobObj is not IBlobUpdateInfo blobUpdateInfo)
            throw new InvalidOperationException("Invalid blob args");
        if (blobUpdateInfo.Stream == null)
            throw new InvalidOperationException("Steam is null");

        var json = ZipUtils.DecompressText(blobUpdateInfo.Stream);
        var data = JsonConvert.DeserializeObject<ExpandoObject>(json)
            ?? throw new InvalidOperationException("Data deserialization fails");

        var prms = _currentUser.DefaultParams();
        await _dbContext.SaveModelAsync(null, "a2meta.[Config.Export.Update]", data, prms);
        return new ExpandoObject();
    }
}

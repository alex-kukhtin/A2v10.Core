// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Services;

namespace A2v10.Metadata;

public class ExportApplicationHandler(IServiceProvider _serviceProvider) : IClrInvokeBlob
{
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    private readonly ICurrentUser _currentUser = _serviceProvider.GetRequiredService<ICurrentUser>();
    public async Task<InvokeBlobResult> InvokeAsync(ExpandoObject args)
    {
        var prms = _currentUser.DefaultParams();
        var dm = await _dbContext.LoadModelAsync(null, "a2meta.[Config.Export]", prms)
            ?? throw new InvalidOperationException("Export application data model is null");

        var apps = dm.Eval<List<ExpandoObject>>("Application")
            ?? throw new InvalidOperationException("Application is null");  
        if (apps.Count != 1)
            throw new InvalidOperationException("Invalid application descriptor");
        var app = apps[0];
        var appName = app.Get<String>("Name");
        var appVersion = app.Get<Int32>("Version");
        var json = JsonConvert.SerializeObject(dm.Root, JsonSettings.DefaultExpando);
        var stream = ZipUtils.CompressText(json);

        return new InvokeBlobResult()
        {
            Name = $"{appName}_{appVersion}.zip",
            Mime = MimeTypes.Application.Zip,
            Stream = stream
        };
    }
}

// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

using System.IO;
using System;
using System.Dynamic;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Core.Web.Site.TestServices;
using A2v10.Services;

namespace A2v10.Core.Web.Site.AppActions;

internal class LoadApplication(IServiceProvider serviceProvider) : IClrInvokeTarget
{
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();
    private readonly TestBusinessAppProvider _appProvider = serviceProvider.GetRequiredService<TestBusinessAppProvider>();
    private readonly ICurrentUser _currentUser = serviceProvider.GetRequiredService<ICurrentUser>();

	public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        if (_currentUser.Identity.Id == null)
            throw new InvalidOperationException("Current user is null");

        var fileName = args.Get<String>("FileName");
        if (String.IsNullOrEmpty(fileName))
            throw new InvalidOperationException("FileName is null");

        var appPath = _appProvider.GetAppFilePath(fileName);
        String? json = null;
        using (var stream = new FileStream(appPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            json = ZipUtils.DecompressText(stream);
        }
        var data = JsonConvert.DeserializeObject<ExpandoObject>(json) ??
            throw new InvalidOperationException("Data is empty");

        var prms = _currentUser.DefaultParams();

        await _dbContext.SaveModelAsync(_currentUser.Identity.Segment, "adm.[Application.Upload.Update]", data, prms);
        return new ExpandoObject()
        {
            { "success", true }
        };
    }
}

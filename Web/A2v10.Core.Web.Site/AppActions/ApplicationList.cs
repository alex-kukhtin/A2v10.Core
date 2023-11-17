// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Text;
using System.Threading.Tasks;
using A2v10.Core.Web.Site.TestServices;
using A2v10.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace A2v10.Core.Web.Site.AppActions;

internal class ApplicationList(IServiceProvider serviceProvider) : IClrInvokeTarget
{
    private readonly TestBusinessAppProvider _appProvider = serviceProvider.GetRequiredService<TestBusinessAppProvider>();

    public Task<Object> InvokeAsync(ExpandoObject args)
    {
        var apps = _appProvider.AllApplications;
        var json = JsonConvert.SerializeObject(apps);
        var result = new InvokeBlobResult()
        {
            Mime = MimeTypes.Application.Json,
            Stream = Encoding.UTF8.GetBytes(json)
        };
        return Task.FromResult<Object>(result);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using A2v10.Infrastructure;
using Microsoft.Extensions.Logging;

namespace A2v10.Platform.Web.Controllers;

[Route("_data/[action]")]
[Route("admin/_data/[action]")]
[ExecutingFilter]
[Authorize]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class DataController : BaseController
{
    private readonly IDataService _dataService;
    private ILogger<DataController> _logger;

    public DataController(IApplicationHost host,
        ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler, IDataService dataService, ILogger<DataController> logger)
        : base(host, localizer, currentUser, profiler) =>
        (_dataService, _logger) = (dataService, logger);

    [HttpPost]
    public async Task<IActionResult> Reload()
    {
        return await TryCatch(async () =>
        {
            var eo = await Request.ExpandoFromBodyAsync();
            if (eo == null)
                throw new InvalidRequestException(Request.Path);
            var baseUrl = eo.Get<String>("baseUrl");
            if (baseUrl == null)
                throw new InvalidRequestException(nameof(Reload));

            String data = await _dataService.ReloadAsync(baseUrl, SetSqlQueryParams);
            return new WebActionResult(data);
        });
    }

    [HttpPost]
    public Task<IActionResult> Expand()
    {
        return TryCatch(async () =>
        {
            var eo = await Request.ExpandoFromBodyAsync();
            if (eo == null)
                throw new InvalidRequestException(Request.Path);

            var expandData = await _dataService.ExpandAsync(eo, SetSqlQueryParams);

            return new WebActionResult(expandData);
        });
    }

    [HttpPost]
    public Task<IActionResult> LoadLazy()
    {
        return TryCatch(async () =>
        {
            var eo = await Request.ExpandoFromBodyAsync();
            if (eo == null)
                throw new InvalidRequestException(Request.Path);

            var lazyData = await _dataService.LoadLazyAsync(eo, SetSqlQueryParams);

            return new WebActionResult(lazyData);
        });
    }

    [HttpPost]
    public Task<IActionResult> Save()
    {
        return TryCatch(async () =>
        {
            var eo = await Request.ExpandoFromBodyAsync();
            if (eo == null)
                throw new InvalidRequestException(Request.Path);
            String? baseUrl = eo.Get<String>("baseUrl");
            if (baseUrl == null)
                throw new InvalidRequestException(nameof(Save));
            ExpandoObject data = eo.GetNotNull<ExpandoObject>("data");

            var savedData = await _dataService.SaveAsync(baseUrl, data, SetSqlQueryParams);

            return new WebActionResult(savedData);
        });
    }

    [HttpPost]
    public Task<IActionResult> Invoke()
    {
        return TryCatch(async () =>
        {
            var eo = await Request.ExpandoFromBodyAsync();
            if (eo == null)
                throw new InvalidRequestException(Request.Path);
            String? baseUrl = eo.Get<String>("baseUrl");
            if (baseUrl == null)
                throw new InvalidRequestException(nameof(Invoke));
            String? cmd = eo.Get<String>("cmd");
            if (cmd == null)
                throw new InvalidRequestException(nameof(Invoke));
            ExpandoObject? data = eo.Get<ExpandoObject>("data");

            var result = await _dataService.InvokeAsync(baseUrl, cmd, data, SetSqlQueryParams);
            return new WebBinaryActionResult(result.Body, result.ContentType);
        });
    }

    [HttpPost]
    public Task<IActionResult> DbRemove()
    {
        return TryCatch(async () =>
        {
            var eo = await Request.ExpandoFromBodyAsync();
            if (eo == null)
                throw new InvalidRequestException(Request.Path);

            var baseUrl = eo.Get<String>("baseUrl");
            if (baseUrl == null)
                throw new InvalidRequestException(nameof(DbRemove));

            Object id = eo.GetNotNull<Object>("id");
            //TODO_V Have to prop derive from a front?
            //String propName = eo.GetNotNull<String>("prop");
            string propName = "";

            await _dataService.DbRemoveAsync(baseUrl, id, propName, SetSqlQueryParams);

            return new WebActionResult("{\"status\": \"OK\"}");
        });
    }

    [HttpPost]
    public async Task<IActionResult> ExportTo()
    {
        var eo = await Request.ExpandoFromBodyAsync();
        if (eo == null)
            throw new InvalidRequestException(Request.Path);
        try
        {
            //String format = eo.Get<String>("format");
            var data = _dataService.Html2Excel(eo.GetNotNull<String>("html"));
            return new WebBinaryActionResult(data, MimeTypes.Application.OctetBinary);
        }
        catch (Exception ex)
        {
            return WriteExceptionStatus(ex);
        }
    }

    private async Task<IActionResult> TryCatch(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{@1}", ex.Data);
            return WriteExceptionStatus(ex);
        }
    }
}

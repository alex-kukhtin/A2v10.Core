// Copyright © 2020-2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Platform.Web.Models;

namespace A2v10.Platform.Web.Controllers;

[Route("_application/[action]")]
[ExecutingFilter]
[Authorize]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ApplicationController(IApplicationHost _host,
    ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler, IDbContext _dbContext) : BaseController(_host, localizer, currentUser, profiler)
{
    
    [HttpPost]
    [ActionName("setperiod")]
    public async Task<IActionResult> SetPeriod()
    {
        var dataToSet = await ReadBodyAsync();
        SetSqlQueryParams(dataToSet);
        await _dbContext.ExecuteExpandoAsync(_host.TenantDataSource, "a2user_state.SetGlobalPeriod", dataToSet);
        return new WebActionResult("{}");
    }

    [HttpPost]
    [ActionName("switchtocompany")]
    public async Task<IActionResult> SЫwitchToCompany()
    {
        var dataToSet = await ReadBodyAsync();

        await SwitchToCompany(dataToSet);
        return new WebActionResult("{}");

    }
    async Task<ExpandoObject> ReadBodyAsync()
    {
        using var tr = new StreamReader(Request.Body, Encoding.UTF8);
        var json = await tr.ReadToEndAsync();
        return JsonConvert.DeserializeObject<ExpandoObject>(json, JsonHelpers.ExpandoObjectSettings)
            ?? throw new InvalidReqestExecption(nameof(SetPeriod));
    }

    public async Task SwitchToCompany(ExpandoObject data)
    {
        if (!_host.IsMultiCompany)
            throw new InvalidOperationException(nameof(SwitchToCompany));
        Int64 CompanyId = data.Get<Int64>("company");
        if (CompanyId == 0)
            throw new InvalidOperationException("Unable to switch to company with id='0'");
        var saveModel = new SwitchToCompanySaveModel()
        {
            UserId = _currentUser.Identity.Id
                ?? throw new InvalidOperationException("UserId is null"),
            TenantId = _currentUser.Identity.Tenant,
            CompanyId = CompanyId,
        };
        await _dbContext.ExecuteAsync(_host.TenantDataSource, "a2security_tenant.SwitchToCompany", saveModel);
        _currentUser.SetCompanyId(CompanyId);
    }
}

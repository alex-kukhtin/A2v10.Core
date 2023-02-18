// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Platform.Web.Controllers;

public class SwitchToCompanySaveModel
{
    public Int64 UserId { get; set; }
    public Int32 TenantId { get; set; }
    public Int64 CompanyId { get; set; }
}

[Route("_application/[action]")]
[ExecutingFilter]
[Authorize]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ApplicationController : BaseController
{
    public ApplicationController(IApplicationHost host,
        ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler)
        : base(host, localizer, currentUser, profiler)
    {
        //do nothing
    }

    [HttpPost]
    public async Task<IActionResult> SwitchToCompany()
    {
        try
        {
            if (!_host.IsMultiCompany)
                throw new InvalidRequestException("SwitchToCompany");
            var data = await Request.ExpandoFromBodyAsync()
                ?? throw new InvalidProgramException("Switch to company. Data is null");
            Int64 CompanyIdToSet = data.GetNotNull<Int64>("company");
            if (CompanyIdToSet == 0)
                throw new InvalidRequestException("Unable to switch to company with id='0'");
            if (_host.IsMultiTenant)
            {
                var saveModel = new SwitchToCompanySaveModel()
                {
                    UserId = UserId,
                    TenantId = TenantId ?? 0,
                    CompanyId = CompanyIdToSet
                };
                await _dbContext.ExecuteAsync<SwitchToCompanySaveModel>(null, "a2security_tenant.SwitchToCompany", saveModel);
            }
            else
            {
                var saveModel = new SwitchToCompanySaveModel()
                {
                    UserId = UserId,
                    CompanyId = CompanyIdToSet
                };
                await _dbContext.ExecuteAsync<SwitchToCompanySaveModel>(null, "a2security.[User.SwitchToCompany]", saveModel);
            }
            _currentUser.SetCompanyId(CompanyIdToSet);
            return new WebActionResult("{\"status\":\"success\"}");
        }
        catch (Exception ex)
        {
            return WriteExceptionStatus(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> SetPeriod()
    {
        try
        {
            var dataToSet = await Request.ExpandoFromBodyAsync()
                ?? throw new InvalidProgramException("SetPeriod. Body is null");
            SetSqlQueryParams(dataToSet);
            await _dbContext.ExecuteExpandoAsync(null, "a2user_state.SetGlobalPeriod", dataToSet);
            return new WebActionResult("{\"status\":\"success\"}");
        }
        catch (Exception ex)
        {
            return WriteExceptionStatus(ex);
        }
    }
}

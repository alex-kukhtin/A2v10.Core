// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Platform.Web.Controllers
{

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
		private readonly IDbContext _dbContext;

		public ApplicationController(IApplicationHost host,
			ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler, IDbContext dbContext)
			: base(host, localizer, currentUser, profiler)
		{
			_dbContext = dbContext;
		}

		[HttpPost]
		public async Task<IActionResult> SwitchToCompany()
		{
			try
			{
				if (!_host.IsMultiCompany)
					throw new InvalidReqestExecption("SwitchToCompany");
				var data = await Request.ExpandoFromBodyAsync();
				Int64 CompanyIdToSet = data.Get<Int64>("company");
				if (CompanyIdToSet == 0)
					throw new InvalidReqestExecption("Unable to switch to company with id='0'");
				if (_host.IsMultiTenant)
				{
					var saveModel = new SwitchToCompanySaveModel()
					{
						UserId = UserId.Value,
						TenantId = TenantId.HasValue ? TenantId.Value : 0,
						CompanyId = CompanyIdToSet
					};
					await _dbContext.ExecuteAsync<SwitchToCompanySaveModel>(null, "a2security_tenant.SwitchToCompany", saveModel);
				}
				else
				{
					var saveModel = new SwitchToCompanySaveModel()
					{
						UserId = UserId.Value,
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
				var dataToSet = await Request.ExpandoFromBodyAsync();
				SetSqlQueryParams(dataToSet);
				await _dbContext.ExecuteExpandoAsync(null, "a2user_state.SetGlobalPeriod", dataToSet);
				return new WebActionResult("{\"status\":\"success\"}");
			}
			catch (Exception ex)
			{
				return WriteExceptionStatus (ex);
			}
		}
	}
}

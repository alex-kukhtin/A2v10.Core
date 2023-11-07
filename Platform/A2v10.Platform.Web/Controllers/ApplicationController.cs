// Copyright © 2020-2022 Oleksandr Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Platform.Web.Controllers;

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
}

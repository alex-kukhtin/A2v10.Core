// Copyright © 2020-2022 Oleksandr Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web.Controllers;

[Route("_application/[action]")]
[ExecutingFilter]
[Authorize]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ApplicationController(IApplicationHost host,
        ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler /*IDbContext dbContext*/) : BaseController(host, localizer, currentUser, profiler)
{
	//private readonly IDbContext _dbContext = dbContext;
}

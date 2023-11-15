// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace A2v10.Platform.Web;

public class CurrentUserMiddleware(RequestDelegate next)
{
	private readonly RequestDelegate _next = next;

    public async Task Invoke(HttpContext context, CurrentUser currentUser)
	{
		currentUser.Setup(context);
		await _next(context);
	}
}

// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace A2v10.Platform.Web
{
	public class CurrentUserMiddleware
	{
		private readonly RequestDelegate _next;

		public CurrentUserMiddleware(RequestDelegate next)
		{
			_next = next;
		}

		public async Task Invoke(HttpContext context, CurrentUser currentUser)
		{
			currentUser.Setup(context);
			await _next(context);
		}
	}
}

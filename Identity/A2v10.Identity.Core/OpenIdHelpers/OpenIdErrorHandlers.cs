// Copyright © 2024-2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;
using System.Linq;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using A2v10.Data.Interfaces;
using A2v10.Web.Identity;
using A2v10.Identity.Core.Helpers;


namespace A2v10.Identity.Core;

public static class OpenIdErrorHandlers
{
	public static Func<RemoteFailureContext, Task> OnRemoteFailure => (context) =>
	{
		if (context.Failure is Exception ex)
		{
			if (ex.Message == "Correlation failed.")
			{
				context.Response.Redirect("/account/login");
				context.HandleResponse();
			}
		}
		return Task.CompletedTask;
	};
}

public static class OpenIdTicketHandler<T> where T : struct
{
    public static async Task OnTickedReceived(TicketReceivedContext context)
    {
        var dbContext = context.Request.HttpContext.RequestServices.GetRequiredService<IDbContext>();
        var options = context.Request.HttpContext.RequestServices.GetRequiredService<IOptions<AppUserStoreOptions<T>>>();

        String? dataSource = options.Value?.DataSource;
        String dbSchema = options.Value?.Schema ?? "a2security";

        var personName = context.Principal?.Identity?.Name;
        var userName = context.Principal?.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;

        var prms = new ExpandoObject();
        prms.Add("PersonName", personName)
            .Add("UserName", userName);
        
        await dbContext.ExecuteExpandoAsync(dataSource, $"{dbSchema}.[User.EnsureExternalLogin]", prms);
    }
}
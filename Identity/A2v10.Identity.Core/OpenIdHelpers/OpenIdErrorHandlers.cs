// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Authentication;

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

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using A2v10.Infrastructure;

namespace A2v10.Identity.UI;

/* Self-registration exists only while 'Application:Registration' says so. The login view hides the
 * link; this closes the door - on every action of the registration, the confirmation included.
 */
public sealed class RegistrationAttribute : ActionFilterAttribute
{
	public override void OnActionExecuting(ActionExecutingContext context)
	{
		if (!context.HttpContext.RequestServices.GetRequiredService<IOptions<AppOptions>>().Value.Registration)
			context.Result = new NotFoundResult();
	}
}

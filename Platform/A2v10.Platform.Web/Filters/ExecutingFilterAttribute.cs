// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Mvc.Filters;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public sealed class ExecutingFilterAttribute : ActionFilterAttribute
	{
		IProfileRequest _request;

		public override void OnActionExecuting(ActionExecutingContext filterContext)
		{
			base.OnActionExecuting(filterContext);
			
			if (filterContext.Controller is IControllerProfiler iCtrlProfiler)
				_request = iCtrlProfiler.BeginRequest();
		}

		public override void OnResultExecuting(ResultExecutingContext context)
		{
			if (context.Controller is not IControllerProfiler iCtrlProfiler)
				return;
			iCtrlProfiler.EndRequest(_request);
			base.OnResultExecuting(context);
		}
	}
}

using A2v10.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Core.Web.Mvc
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public sealed class ExecutingFilterAttribute : ActionFilterAttribute
	{
		IProfileRequest _request;

		public override void OnActionExecuting(ActionExecutingContext filterContext)
		{
			base.OnActionExecuting(filterContext);
			
			if (filterContext.Controller is IControllerTenant iCtrlTenant)
				iCtrlTenant.StartTenant();

			if (filterContext.Controller is IControllerAdmin iCtrlAdmin)
			{
				if (filterContext.HttpContext.Request.Path.StartsWithSegments("/admin"))
					iCtrlAdmin.SetAdmin();
			}

			if (filterContext.Controller is IControllerProfiler iCtrlProfiler)
				_request = iCtrlProfiler.BeginRequest();
		}

		public override void OnResultExecuted(ResultExecutedContext filterContext)
		{
			base.OnResultExecuted(filterContext);
			if (filterContext.Controller is not IControllerProfiler iCtrlProfiler)
				return;
			iCtrlProfiler.EndRequest(_request);
		}
	}
}

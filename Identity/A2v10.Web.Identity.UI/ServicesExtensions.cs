// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using Microsoft.Extensions.DependencyInjection;

namespace A2v10.Web.Identity.UI
{
	public static class ServicesExtensions
	{

		public static IMvcBuilder AddDefaultIdentityUI(this IMvcBuilder builder)
		{
			var assembly = typeof(AccountController).Assembly;
			builder.AddApplicationPart(assembly);
			return builder;
		}
	}
}

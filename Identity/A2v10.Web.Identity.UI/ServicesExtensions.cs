// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.


using A2v10.Web.Identity.UI;

namespace Microsoft.Extensions.DependencyInjection
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

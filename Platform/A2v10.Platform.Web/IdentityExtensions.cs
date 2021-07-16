// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Security.Claims;
using System.Security.Principal;

using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Platform.Web
{
	public class IdentityUserInfo : IUserInfo
	{
		public Int64 UserId { get; set; }
		public Boolean IsAdmin { get; set; }
		public Boolean IsTenantAdmin { get; set; }
	}


	public static class IdentityExtensions
	{

		public static IUserInfo UserInfo(this IIdentity identity)
		{
			if (identity == null)
				return null;
			if (identity is not ClaimsIdentity user)
				return null;

			var ui = new IdentityUserInfo()
			{
				UserId = identity.GetUserId<Int64>()
			};

			var value = user?.FindFirst(WellKnownClims.Admin)?.Value;
			ui.IsAdmin = value == WellKnownClims.Admin;

			value = user?.FindFirst(WellKnownClims.TenantAdmin)?.Value;
			ui.IsTenantAdmin = value == WellKnownClims.TenantAdmin;
			return ui;
		}
	}
}

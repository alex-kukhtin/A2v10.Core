// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.Globalization;
using System.Security.Claims;
using System.Security.Principal;

using A2v10.Infrastructure;

namespace A2v10.Web.Identity
{
	public class IdentityUserInfo : IUserInfo
	{
		public Int64 UserId { get; set; }
		public Boolean IsAdmin { get; set; }
		public Boolean IsTenantAdmin { get; set; }
	}

	public static class WellKnownClims
	{
		public const String NameIdentifier =  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
		public const String PersonName = "PersonName";
	}

	public static class IdentityExtensions
	{
		/*TODO:
		public static String GetUserPersonName(this IIdentity identity)
		{
			if (!(identity is ClaimsIdentity user))
				return null;
			var value = user.FindFirst("PersonName").Value;
			return String.IsNullOrEmpty(value) ? identity.GetUserName() : value;
		}
		*/

		public static Boolean IsUserAdmin(this IIdentity identity)
		{
			if (identity is not ClaimsIdentity user)
				return false;
			var value = user?.FindFirst("Admin")?.Value;
			return value == "Admin";
		}

		public static String GetUserClientId(this IIdentity identity)
		{
			if (identity is not ClaimsIdentity user)
				return null;
			var value = user.FindFirst("ClientId").Value;
			return String.IsNullOrEmpty(value) ? null : value;
		}

		public static Boolean IsTenantAdmin(this IIdentity identity)
		{
			if (identity is not ClaimsIdentity user)
				return false;
			var value = user.FindFirst("TenantAdmin").Value;
			return value == "TenantAdmin";
		}

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

			var value = user?.FindFirst("Admin")?.Value;
			ui.IsAdmin = value == "Admin";

			value = user?.FindFirst("TenantAdmin")?.Value;
			ui.IsTenantAdmin = value == "TenantAdmin";
			return ui;
		}

		public static Int32 GetUserTenantId(this IIdentity identity)
		{
			if (identity == null)
				return 0;
			if (identity is not ClaimsIdentity user)
				return 0;
			var value = user.FindFirst("TenantId").Value;
			if (Int32.TryParse(value, out Int32 tenantId))
				return tenantId;
			return 0;
		}

		public static T GetUserId<T>(this IIdentity identity)
		{
			if (identity == null)
				return default;
			if (identity is not ClaimsIdentity user)
				return default;
			var claim = user?.FindFirst(WellKnownClims.NameIdentifier)?.Value;
			if (claim == null)
				return default;
			return (T) Convert.ChangeType(claim, typeof(T), CultureInfo.InvariantCulture);
		}

		public static String GetUserSegment(this IIdentity identity)
		{
			if (identity == null)
				return null;
			if (identity is not ClaimsIdentity user)
				return null;
			return user.FindFirst("Segment").Value;
		}

		public static String GetUserClaim(this IIdentity identity, String claim)
		{
			if (identity is not ClaimsIdentity user)
				return null;
			return user.FindFirst(claim)?.Value;
		}
	}
}

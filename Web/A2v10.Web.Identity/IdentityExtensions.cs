// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

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
		public const String Admin = "Admin";
		public const String TenantAdmin = "TenantAdmin";
		public const String ClientId = "ClientId";
		public const String TenantId = "TenantId";
		public const String Segment = "Segment";
	}

	public static class IdentityExtensions
	{

		public static T GetUserId<T>(this IIdentity identity)
		{
			if (identity == null)
				return default;
			if (identity is not ClaimsIdentity user)
				return default;
			var claim = user?.FindFirst(WellKnownClims.NameIdentifier)?.Value;
			if (claim == null)
				return default;
			return (T)Convert.ChangeType(claim, typeof(T), CultureInfo.InvariantCulture);
		}

		public static String GetUserClaim(this IIdentity identity, String claim)
		{
			if (identity is not ClaimsIdentity user)
				return null;
			return user.FindFirst(claim)?.Value;
		}

		public static String GetUserPersonName(this IIdentity identity)
		{
			var claim = identity.GetUserClaim(WellKnownClims.PersonName);
			return String.IsNullOrEmpty(claim) ? identity.Name : claim;
		}

		public static Boolean IsUserAdmin(this IIdentity identity)
		{
			var claim = identity.GetUserClaim(WellKnownClims.Admin);
			return claim == "Admin";
		}

		public static String GetUserClientId(this IIdentity identity)
		{
			return identity.GetUserClaim(WellKnownClims.ClientId);
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
			var value = user.FindFirst(WellKnownClims.TenantId)?.Value;
			if (value == null)
				return 0;
			if (Int32.TryParse(value, out Int32 tenantId))
				return tenantId;
			return 0;
		}

		public static String GetUserSegment(this IIdentity identity)
		{
			if (identity == null)
				return null;
			if (identity is not ClaimsIdentity user)
				return null;
			return user.FindFirst("Segment").Value;
		}
	}
}

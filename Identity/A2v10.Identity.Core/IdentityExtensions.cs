// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using System;
using System.Globalization;
using System.Security.Claims;
using System.Security.Principal;

namespace A2v10.Web.Identity
{
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
			var tp = typeof(T);
			if (tp.IsNullableType())
			{
				tp = Nullable.GetUnderlyingType(tp);
			}
			return (T) Convert.ChangeType(claim, tp, CultureInfo.InvariantCulture);
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
			return claim == WellKnownClims.Admin;
		}

		public static String GetUserClientId(this IIdentity identity)
		{
			return identity.GetUserClaim(WellKnownClims.ClientId);
		}

		public static String GetUserLocale(this IIdentity identity)
		{
			return identity.GetUserClaim(WellKnownClims.Locale);
		}

		public static Boolean IsTenantAdmin(this IIdentity identity)
		{
			if (identity is not ClaimsIdentity user)
				return false;
			var value = user.FindFirst(WellKnownClims.TenantAdmin)?.Value;
			if (value == null)
				return false;
			return value == WellKnownClims.TenantAdmin;
		}

		public static Int32? GetUserTenantId(this IIdentity identity)
		{
			if (identity == null)
				return null;
			if (identity is not ClaimsIdentity user)
				return null;
			var value = user.FindFirst(WellKnownClims.TenantId)?.Value;
			if (value == null)
				return null;
			if (Int32.TryParse(value, out Int32 tenantId))
				return tenantId;
			return null;
		}

		public static String GetUserSegment(this IIdentity identity)
		{
			return identity.GetUserClaim(WellKnownClims.Segment);
		}
	}
}

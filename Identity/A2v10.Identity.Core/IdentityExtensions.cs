// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System.Globalization;
using System.Security.Claims;
using System.Security.Principal;

namespace A2v10.Web.Identity;
public static class IdentityExtensions
{ 
	private static Boolean IsNullableType(this Type type)
	{
		return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
	}

	public static T? GetUserId<T>(this IIdentity? identity)
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
			tp = Nullable.GetUnderlyingType(tp);
		return (T) Convert.ChangeType(claim, tp!, CultureInfo.InvariantCulture);
	}

	public static String? GetUserClaim(this IIdentity? identity, String claim)
	{
		if (identity is not ClaimsIdentity user)
			return null;
		return user?.FindFirst(claim)?.Value;
	}

	public static String? GetUserPersonName(this IIdentity? identity)
	{
		var claim = identity.GetUserClaim(WellKnownClims.PersonName);
		return String.IsNullOrEmpty(claim) ? identity?.Name : claim;
	}

	public static Boolean IsUserAdmin(this IIdentity? identity)
	{
		var claim = identity?.GetUserClaim(WellKnownClims.Admin);
		return claim == WellKnownClims.Admin;
	}

	public static String? GetUserClientId(this IIdentity? identity)
	{
		return identity?.GetUserClaim(WellKnownClims.ClientId);
	}

	public static String? GetUserLocale(this IIdentity? identity)
	{
		return identity?.GetUserClaim(WellKnownClims.Locale);
	}

	public static Boolean IsTenantAdmin(this IIdentity? identity)
	{
		if (identity is not ClaimsIdentity user)
			return false;
		var value = user.FindFirst(WellKnownClims.TenantAdmin)?.Value;
		if (value == null)
			return false;
		return value == WellKnownClims.TenantAdmin;
	}

	public static Int32? GetUserTenantId(this IIdentity? identity)
	{
		var tenant = identity?.GetUserClaim(WellKnownClims.Segment);
		if (tenant == null)
			return null;
		if (Int32.TryParse(tenant, out Int32 tenantId))
			return tenantId == 0 ? null : tenantId;
		return null;
	}

	public static String? GetUserSegment(this IIdentity? identity)
	{
		return identity?.GetUserClaim(WellKnownClims.Segment);
	}

	public static Int64 GetUserOrganization(this IIdentity? identity)
	{
		var org = identity?.GetUserClaim(WellKnownClims.Organization);
		if (String.IsNullOrEmpty(org))
			return 0;
		if (Int64.TryParse(org, out Int64 result))
			return result;
		return 0;
	}
}


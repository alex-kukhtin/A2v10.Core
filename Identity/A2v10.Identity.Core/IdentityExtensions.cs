// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.ComponentModel;
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
		return identity.GetClaimValue<T>(WellKnownClaims.NameIdentifier);
	}

	public static T? GetClaimValue<T>(this IIdentity? identity, String claimName)
	{
		if (identity == null)
			return default;
		if (identity is not ClaimsIdentity user)
			return default;
		var claim = user?.FindFirst(claimName)?.Value;
		var tp = typeof(T);
		if (tp.IsNullableType())
			tp = Nullable.GetUnderlyingType(tp);
		if (claim == null)
			return default;
		return (T?) TypeDescriptor.GetConverter(tp!).ConvertFromInvariantString(claim); 
	}

	public static String? GetUserClaim(this IIdentity? identity, String claim)
	{
		if (identity is not ClaimsIdentity user)
			return null;
		return user?.FindFirst(claim)?.Value;
	}

	public static String? GetUserPersonName(this IIdentity? identity)
	{
		var claim = identity.GetUserClaim(WellKnownClaims.PersonName);
		return String.IsNullOrEmpty(claim) ? identity?.Name : claim;
	}

	public static String? GetUserFirstName(this IIdentity? identity)
	{
		return identity.GetUserClaim(WellKnownClaims.FirstName);
	}

	public static String? GetUserLastName(this IIdentity? identity)
	{
		return identity.GetUserClaim(WellKnownClaims.LastName);
	}

	public static Boolean IsUserAdmin(this IIdentity? identity)
	{
		var claim = identity?.GetUserClaim(WellKnownClaims.Admin);
		return claim == WellKnownClaims.Admin;
	}

	public static String? GetUserClientId(this IIdentity? identity)
	{
		return identity?.GetUserClaim(WellKnownClaims.ClientId);
	}

	public static String? GetUserRoles(this IIdentity? identity)
	{
		return identity?.GetUserClaim(WellKnownClaims.Roles);
	}

	public static IEnumerable<String> GetUserRolesList(this IIdentity? identity, Char separator = ',')
	{
		var roles = identity?.GetUserClaim(WellKnownClaims.Roles);
		if (String.IsNullOrEmpty(roles))
			yield break;
		foreach (var r in roles.Split(separator))
			yield return r;
	}

	public static String? GetUserLocale(this IIdentity? identity)
	{
		return identity?.GetUserClaim(WellKnownClaims.Locale);
	}

	public static Boolean IsTenantAdmin(this IIdentity? identity)
	{
		if (identity is not ClaimsIdentity user)
			return false;
		var value = user.FindFirst(WellKnownClaims.TenantAdmin)?.Value;
		if (value == null)
			return false;
		return value == WellKnownClaims.TenantAdmin;
	}

	public static T? GetUserTenant<T>(this IIdentity? identity)
	{
		return identity.GetClaimValue<T>(WellKnownClaims.Tenant);
	}
	public static T? GetUserOrganization<T>(this IIdentity? identity)
	{
		return identity.GetClaimValue<T>(WellKnownClaims.Organization);
	}
	public static T? GetUserBranch<T>(this IIdentity? identity)
	{
		return identity.GetClaimValue<T>(WellKnownClaims.Branch);
	}

	public static String? GetUserOrganizationKey(this IIdentity? identity)
	{
		return identity?.GetUserClaim(WellKnownClaims.OrganizationKey);
	}

	public static String? GetUserSegment(this IIdentity? identity)
	{
		return identity?.GetUserClaim(WellKnownClaims.Segment);
	}
}


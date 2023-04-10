// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Web.Identity;
public static class WellKnownClaims
{
	public const String NameIdentifier =  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
	public const String Name = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
	public const String PersonName = nameof(PersonName);
	public const String FirstName = nameof(FirstName);
	public const String LastName = nameof(LastName);
	public const String Admin = nameof(Admin);
	public const String TenantAdmin = nameof(TenantAdmin);
	public const String ClientId = nameof(ClientId);
	public const String Tenant = nameof(Tenant);
	public const String Segment = nameof(Segment);
	public const String Locale = nameof(Locale);
	public const String Organization = nameof(Organization);
	public const String OrganizationKey = nameof(OrganizationKey);
	public const String Branch = nameof(Branch);
	public const String Roles = nameof(Roles);
	public const String IsPersistent = nameof(IsPersistent);
}


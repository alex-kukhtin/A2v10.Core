// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;

namespace A2v10.Web.Identity;

public class AppUser<T> where T : struct
{
	public T Id { get; set; }
	public String? UserName { get; set; }
	public String? PersonName { get; set; }
	public String? FirstName { get; set; }
	public String? LastName { get; set; }
	public String? Email { get; set; }
	public String? PhoneNumber { get; set; }

	public String? PasswordHash { get; set; }
	public String? SecurityStamp { get; set; }
	public DateTimeOffset LockoutEndDateUtc { get; set; }
	public Boolean LockoutEnabled { get; set; }
	public Int32 AccessFailedCount { get; set; }
	public Boolean EmailConfirmed { get; set; }
	public Boolean PhoneNumberConfirmed { get; set; }
	public T? Tenant { get; set; }
	public String? Segment { get; set; }
	public String? Locale { get; set; }
	public T? Organization { get; set; }
	public String? OrganizationKey { get; set; }
	public DateOnly BirthDate { get; set; }
	public Boolean SetPassword { get; set; }
	public Boolean IsPersistent { get; set; }
	public String? Roles { get; set; }

	// for .net framework compatibility
	public String? PasswordHash2 { get; set; }
	public String? SecurityStamp2 { get; set; }

	public Boolean IsEmpty => EqualityComparer<T>.Default.Equals(Id, default);

}


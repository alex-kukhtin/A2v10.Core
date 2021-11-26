// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

namespace A2v10.Web.Identity;

public class AppUser
{
	public Int64 Id { get; set; }
	public String? UserName { get; set; }
	public String? PersonName { get; set; }
	public String? Email { get; set; }
	public String? PhoneNumber { get; set; }

	public String? PasswordHash { get; set; }
	public String? SecurityStamp { get; set; }
	public DateTimeOffset LockoutEndDateUtc { get; set; }
	public Boolean LockoutEnabled { get; set; }
	public Int32 AccessFailedCount { get; set; }
	public Boolean EmailConfirmed { get; set; }
	public Boolean PhoneNumberConfirmed { get; set; }

	public Int32 Tenant { get; set; }
	public String? Segment { get; set; }
	public String? Locale { get; set; }

	// for .net framework compatibility
	public String? PasswordHash2 { get; set; }
	public String? SecurityStamp2 { get; set; }
}


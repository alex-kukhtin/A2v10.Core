// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;

namespace A2v10.Web.Identity;

[Flags]
public enum UpdateFlags
{
	PersonName = 0x1,
	Email = 0x2,
	FirstName = 0x4,
	LastName = 0x8,
	PhoneNumber = 0x10,
	EmailConfirmed = 0x20,
	PhoneNumberConfirmed = 0x40,
	Roles = 0x80,
	Branch = 0x100,
	ZipCode = 0x200,
	Locale = 0x400,
	TwoFactor = 0x800,
	ExternalId = 0x1000,
	NickName = 0x2000
}
public class AppUser<T> where T : struct
{
	public T Id { get; set; }
	public String? UserName { get; set; }
	public String? PersonName { get; set; }
    public String? NickName { get; set; }
    public String? FirstName { get; set; }
	public String? LastName { get; set; }
	public String? Email { get; set; }
	public String? PhoneNumber { get; set; }

	public String? PasswordHash { get; set; }
	public String? SecurityStamp { get; set; }
	public DateTimeOffset? LockoutEndDateUtc { get; set; }
	public Boolean LockoutEnabled { get; set; }
	public Int32 AccessFailedCount { get; set; }
	public Boolean EmailConfirmed { get; set; }
	public Boolean PhoneNumberConfirmed { get; set; }
	public T? Tenant { get; set; }
	public String? Segment { get; set; }
	public String? Locale { get; set; }
	public T? Organization { get; set; }
	public T? Branch { get; set; }
	public String? OrganizationKey { get; set; }
	public String? OrganizationTag { get; set; }
	public DateOnly BirthDate { get; set; }
	public Boolean SetPassword { get; set; }
	public Boolean IsPersistent { get; set; }
	public Boolean ChangePasswordEnabled { get; set; }
	public String? Roles { get; set; }
    public String? ZipCode { get; set; }
	public String? ExternalId { get; set; }
	public Boolean IsBlocked { get; set; }
	public Boolean TwoFactorEnabled { get; set; }

	// for .net framework compatibility
	public String? PasswordHash2 { get; set; }
	public String? SecurityStamp2 { get; set; }
    public String? Memo { get; set; }
	public String? AuthenticatorKey { get; set; }
    public Boolean IsEmpty => EqualityComparer<T>.Default.Equals(Id, default);
	public UpdateFlags Flags { get; set; }

}


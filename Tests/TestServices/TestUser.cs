// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using A2v10.Infrastructure;

namespace TestServices;

// A user of id 1 with the given roles; readOnly and permitted answer the state and every permission
internal sealed class TestUser(IEnumerable<String> roles, Boolean readOnly = false, Boolean permitted = true) : ICurrentUser
{
	private sealed class UserIdentity(IEnumerable<String> roles) : IUserIdentity
	{
		public Int64? Id => 1;
		public String? Name => "user";
		public String? PersonName => null;
		public Int32? Tenant => null;
		public String? Segment => null;
		public Boolean IsAdmin => false;
		public Boolean IsTenantAdmin => false;
		public String? Theme => null;
		public void SetInitialTenantId(Int32 tenant) { }
		public IEnumerable<String>? Roles => roles;
	}
	private sealed class UserState(Boolean readOnly) : IUserState
	{
		public Int64? Company => null;
		public Boolean IsReadOnly => readOnly;
		public IEnumerable<Guid> Modules => [];
	}
	public IUserIdentity Identity { get; } = new UserIdentity(roles);
	public IUserState State { get; } = new UserState(readOnly);
	public IUserLocale Locale => throw new NotSupportedException();
	public void SetCompanyId(Int64 id) { }
	public void SetInitialTenantId(Int32 tenantId) { }
	public void SetUserState(Boolean admin, Boolean readOnly, String? permissions) { }
	public void AddModules(IEnumerable<Guid> modules) { }
	public ExpandoObject DefaultParams() => [];
	public Boolean IsPermissionEnabled(String key, PermissionFlag flag) => permitted;
}

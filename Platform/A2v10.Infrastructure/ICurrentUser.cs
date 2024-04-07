// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;

namespace A2v10.Infrastructure;
public interface IUserIdentity
{
	Int64? Id { get; }
	String? Name { get; }
	String? PersonName { get; }

	Int32? Tenant { get; }
	String? Segment { get; }

	Boolean IsAdmin { get; }
	Boolean IsTenantAdmin { get; }

	void SetInitialTenantId(Int32 tenant);
	IEnumerable<String>? Roles { get; }
}

public interface IUserState
{
	Int64? Company { get; }
	Boolean IsReadOnly { get; }
	IEnumerable<Guid> Modules { get; }
}

public interface IUserLocale
{
	String Locale { get; }
	String Language { get; }
}

public interface ICurrentUser
{
	public IUserIdentity Identity { get; }
	public IUserState State { get; }
	public IUserLocale Locale { get; }
	void SetCompanyId(Int64 id);
	void SetInitialTenantId(Int32 tenantId);
	void SetUserState(Boolean admin, Boolean readOnly, String? permissions);
	void AddModules(IEnumerable<Guid> modules);
	ExpandoObject DefaultParams();
	Boolean IsPermissionEnabled(String key, PermissionFlag flag);
}


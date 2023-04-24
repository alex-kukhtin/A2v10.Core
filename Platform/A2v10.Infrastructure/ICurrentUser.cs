// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

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
	public Boolean IsAdminApplication { get; }

	void SetCompanyId(Int64 id);
	void SetReadOnly(Boolean readOnly);
	void AddModules(IEnumerable<Guid> modules);
}


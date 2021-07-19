// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure
{
	public interface IUserIdentity
	{
		Int64? Id { get; }
		String Name { get; }
		String PersonName { get; }

		Int32? Tenant { get; }
		String Segment { get; }

		Boolean IsAdmin { get; }
		Boolean IsTenantAdmin { get; }
	}

	public interface IUserState
	{
		Int64? Company { get; }
		Boolean IsReadOnly { get; }
	}

	public interface ICurrentUser
	{
		public IUserIdentity Identity { get; }
		public IUserState State { get; }
		public Boolean IsAdminApplication { get; }
	}
}

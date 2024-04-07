// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;

public class NullPermissionBag : IPermissionBag
{
	public Task LoadPermisionBagAsync(IDbContext _1 /*dbContext*/, String? _2 /*dataSource*/)
	{
		return Task.FromResult(0);	
	}

	Dictionary<String, PermissionFlag> IPermissionBag.DecodePermissions(String? _ /*permissions*/)
	{
		throw new InvalidOperationException("You forgot to call services.UsePermissions()");
	}
}

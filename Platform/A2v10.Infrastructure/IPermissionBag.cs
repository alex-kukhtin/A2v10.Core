// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;

[Flags]
public enum PermissionFlag
{
	CanView = 1,
	CanEdit = 2,
	CanDelete = 4,
	CanApply = 8,
	CanUnapply = 16,
	CanCreate = 32,
	CanFlag64 = 64,
	CanFlag128 = 128,
	CanFlag256 = 256,
}

public interface IPermissionBag
{
	Task LoadPermisionBagAsync(IDbContext dbContext, String? dataSource);
	Dictionary<String, PermissionFlag> DecodePermissions(String? permissions);
}

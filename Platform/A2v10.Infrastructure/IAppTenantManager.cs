// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface IAppTenantManager
{
	Task RegisterUserComplete(Int64 userId);
}

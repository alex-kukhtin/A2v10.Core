// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace A2v10.Module.Infrastructure;

public interface ILicenseManager
{
	Task<Boolean> VerifyLicensesAsync(String? dataSource, Int32? tenantId, IEnumerable<Guid> modules);
}

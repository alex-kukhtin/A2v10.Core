
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace A2v10.App.Abstractions;

public interface ILicenseManager
{
	Task<Boolean> VerifyLicensesAsync(String? dataSource, Int32? tenantId, IEnumerable<Guid> modules);
}

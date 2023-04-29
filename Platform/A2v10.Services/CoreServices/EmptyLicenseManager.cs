// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

using A2v10.Module.Infrastructure;

namespace A2v10.Services;

public class EmptyLicenseManager : ILicenseManager	
{
	public Task<Boolean> VerifyLicensesAsync(String? _1/*dataSource*/, Int32? _2/*tenantId*/, IEnumerable<Guid> _3/*modules*/)
	{
		throw new NotImplementedException(nameof(VerifyLicensesAsync));
	}
}

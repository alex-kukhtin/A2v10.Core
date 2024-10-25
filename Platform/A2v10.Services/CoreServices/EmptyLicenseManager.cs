// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

namespace A2v10.Services;

public class EmptyLicenseManager : ILicenseManager	
{
	public Task<LicenseState> VerifyLicensesAsync(String? _1/*dataSource*/, Int32? _2/*tenantId*/, IEnumerable<Guid> _3/*modules*/)
	{
		throw new NotImplementedException(nameof(VerifyLicensesAsync));
	}

	public Task<ILicenseInfo> GetLicenseInfoAsync(String? dataSource, Int32? tenantId)
	{
		throw new NotImplementedException(nameof(GetLicenseInfoAsync));
	}
}

// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

namespace A2v10.Services;

public class NullLicenseManager : ILicenseManager	
{
	public Task<LicenseState> VerifyLicensesAsync(String? _1/*dataSource*/, Int32? _2/*tenantId*/, IEnumerable<Guid> _3/*modules*/)
	{
		return Task.FromResult<LicenseState>(LicenseState.Ok);
	}

	public Task<ILicenseInfo> GetLicenseInfoAsync(String? dataSource, Int32? tenantId)
	{
		throw new NotImplementedException(nameof(GetLicenseInfoAsync));
	}
}

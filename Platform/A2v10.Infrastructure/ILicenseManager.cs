// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public enum LicenseState
{
	Ok,
	NotFound,
	InvalidFile,
	Expired,
	TermsViolation,
	InvalidAppVersion
}

public interface ILicenseInfo
{
	LicenseState LicenseState { get; }
	String ApplicationName { get; }
    DateTime IssuedOn { get; }
	DateTime ExpiresOn { get; }
	String? Message { get; }	
	String? Title { get; }
	ExpandoObject Data { get; }
}


public interface ILicenseManager
{
	Task<LicenseState> VerifyLicensesAsync(String? dataSource, Int32? tenantId, IEnumerable<Guid> modules);
	Task<ILicenseInfo> GetLicenseInfoAsync(String? dataSource, Int32? tenantId);
}

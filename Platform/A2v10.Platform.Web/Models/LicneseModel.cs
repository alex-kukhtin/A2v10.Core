// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;
public class LicenseModel : ErrorViewModel
{
	public LicenseModel(ILicenseInfo licInfo)
	{
		LicenseInfo = licInfo;	
	}
	public ILicenseInfo LicenseInfo { get; }	
	public Boolean CanContinue => LicenseInfo.LicenseState == LicenseState.Expired || LicenseInfo.LicenseState == LicenseState.TermsViolation;
	public Boolean CanReload => LicenseInfo.LicenseState == LicenseState.NotFound || LicenseInfo.LicenseState == LicenseState.InvalidFile;
}


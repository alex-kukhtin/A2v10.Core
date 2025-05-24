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


public class ViewLicenseModel(ILicenseInfo _licInfo)
{
	public String? Id => _licInfo.Data.Get<String>(nameof(Id));
    public String? Name => _licInfo.Data.Get<String>(nameof(Name));
    public String? PartnerName => _licInfo.Data.Get<String>(nameof(PartnerName));
    public String? Phone => _licInfo.Data.Get<String>(nameof(Phone));
	public String? Email => _licInfo.Data.Get<String>(nameof(Email));
    public String? CodeUA => _licInfo.Data.Get<String>(nameof(CodeUA));
    public String? ExpiresOn => _licInfo.ExpiresOn.ToString("d");
    public String? IssuedOn => _licInfo.IssuedOn.ToString("d");
    public Int64 Users => _licInfo.Data.Get<Int64>(nameof(Users));
    public Int64 Companies => _licInfo.Data.Get<Int64>(nameof(Companies));
}

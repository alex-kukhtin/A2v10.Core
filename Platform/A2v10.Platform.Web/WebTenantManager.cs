// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using Microsoft.Extensions.Options;

namespace A2v10.Platform.Web;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;


public record TenantInfo : ITenantInfo
{
	public String Procedure => "[a2security].[SetTenantId]";
	public String ParamName => "@TenantId";

	public Int32 TenantId { get; init; }
}


public class WebTenantManager : ITenantManager
{
	private readonly ICurrentUser _currentUser;
	private readonly AppOptions _appOptions;
	public WebTenantManager(ICurrentUser currentUser, IOptions<AppOptions> appOptions)
    {
		_currentUser = currentUser;
		_appOptions = appOptions.Value;
	}

	#region ITenantManager
	public ITenantInfo? GetTenantInfo(String? source)
	{
		if (!_appOptions.MultiTenant)
			return null;
		if (source == "Catalog") //TODO: ???
			return null;
		if (!_currentUser.Identity.Tenant.HasValue)
			throw new InvalidOperationException("There is no TenantId");
		return new TenantInfo()
		{
			TenantId = _currentUser.Identity.Tenant.Value
		};
	}
	#endregion
}

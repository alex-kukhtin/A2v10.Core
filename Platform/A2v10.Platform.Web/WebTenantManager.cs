// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Options;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Platform.Web;

public record TenantInfo : ITenantInfo
{
	private readonly String _schema;
	private readonly Object _tenantId;
	public TenantInfo(Object tenantId, String? schema)
	{
		_tenantId = tenantId;
		_schema = schema ?? "a2security";
	}
	public String Procedure => $"[{_schema}].[SetTenantId]";

	public IEnumerable<TenantInfoParam> Params =>
		new List<TenantInfoParam>() {
			new TenantInfoParam("@TenantId", _tenantId)
		};
}

public class WebTenantManager : ITenantManager
{
	private readonly ICurrentUser _currentUser;
	private readonly AppOptions _appOptions;
	private readonly AppUserStoreOptions<Int64> _userStoreOptions;
	public WebTenantManager(ICurrentUser currentUser, IOptions<AppOptions> appOptions,
		IOptions<AppUserStoreOptions<Int64>> userStoreOptions)
    {
		_currentUser = currentUser;
		_appOptions = appOptions.Value;
		_userStoreOptions = userStoreOptions.Value;
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
		return new TenantInfo(_currentUser.Identity.Tenant.Value, _userStoreOptions.Schema);
	}
	#endregion
}

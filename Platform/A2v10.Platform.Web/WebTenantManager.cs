// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

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
		[
			new("@TenantId", _tenantId)
		];
}

public class WebTenantManager(ICurrentUser currentUser, IOptions<AppOptions> appOptions,
    IOptions<AppUserStoreOptions<Int64>> userStoreOptions) : ITenantManager
{
	private readonly ICurrentUser _currentUser = currentUser;
	private readonly AppOptions _appOptions = appOptions.Value;
	private readonly AppUserStoreOptions<Int64> _userStoreOptions = userStoreOptions.Value;

    #region ITenantManager
    public ITenantInfo? GetTenantInfo(String? source)
	{
		if (!_appOptions.MultiTenant)
			return null;
		if (source == _userStoreOptions.DataSource)
			return null;
		if (_currentUser.Identity.Tenant.HasValue)
			return new TenantInfo(_currentUser.Identity.Tenant.Value, _userStoreOptions.Schema);
		return null; // not tenant info, system processor
	}
	#endregion
}

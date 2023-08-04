// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;

using Microsoft.Extensions.Options;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Platform.Web;

public class AppTenantManager : IAppTenantManager
{
	private readonly IDbContext _dbContext;
	private readonly String _dbSchema;
	private readonly String? _dataSource;
	private readonly ICurrentUser _currentUser;
	private readonly Boolean _multiTenant;
	public AppTenantManager(IDbContext dbContext, IOptions<AppUserStoreOptions<Int64>> userStoreOptions,
		ICurrentUser currentUser)
	{
		_dbContext = dbContext;
		var options = userStoreOptions.Value;
		_multiTenant = options.MultiTenant ?? false;
		_dbSchema = options.Schema ?? "a2security";
		_dataSource = options.DataSource;
		_currentUser = currentUser;	
	}
	public async Task RegisterUserComplete(Int64 userId)
	{
		var appUser = await _dbContext.LoadAsync<AppUser<Int64>>(_dataSource, 
				$"[{_dbSchema}].[User.RegisterComplete]",
				new ExpandoObject() { { "Id", userId } })
			 ?? throw new InvalidOperationException("User not found");
		_currentUser.SetInitialTenantId((Int32) (appUser.Tenant ?? 1));
		if (_multiTenant)
			await _dbContext.ExecuteAsync<AppUser<Int64>>(appUser.Segment, $"[{_dbSchema}].[User.CreateTenant]", appUser);
	}
}

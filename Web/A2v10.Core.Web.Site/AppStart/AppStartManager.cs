// Copyright © 2022 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Core.Web.Site;
public class AppStartManager : IAppStartManager
{
	private readonly IDbContext _dbContext;
	public AppStartManager(IDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public Task<Int64?> GetRootMenuId()
	{
		return Task.FromResult<Int64?>(null);
	}
}

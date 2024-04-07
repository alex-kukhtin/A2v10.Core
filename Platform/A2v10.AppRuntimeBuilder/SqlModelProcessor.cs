// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;
using System.Dynamic;
using System.Threading.Tasks;
using System.Data.Common;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

using A2v10.Data.Core;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.AppRuntimeBuilder;

internal class SqlModelProcessor(ICurrentUser _currentUser, IOptions<AppOptions> _options, IDbContext _dbContext)
{
	private readonly Boolean IsMultiTenant = _options.Value.MultiTenant;
	public Task<IDataModel> LoadModelAsync(IPlatformUrl platformUrl, IModelView view, RuntimeTable table)
	{
		return view.IsIndex ? LoadIndexModelAsync(table) : LoadPlainModelAsync(platformUrl, table);
	}
	private void AddDefaultParameters(DbParameterCollection prms)
	{
		prms.AddBigInt("@UserId", _currentUser.Identity.Id);
		// TODO: Check MultiTenant!!!
		prms.AddInt("@TenantId", _currentUser.Identity.Tenant ?? 1);
	}

	public async Task<ExpandoObject> SaveAsync(RuntimeTable table, ExpandoObject data)
	{
		var sqlString = """

		declare @rtable table(Id bigint);
		declare @id bigint;
		merge cat.Agents as t
		using @Agent as s
		on t.TenantId = @TenantId and t.Id = s.Id
		when matched then update set
			t.[Name] = s.[Name],
			t.Memo = s.Memo 
		output inserted.Id into @rtable(Id);
		select top(1) @id = Id from @rtable;

		select [Agent!TAgent!Object] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, a.Memo
		from cat.Agents a where TenantId = @TenantId and Id = @id;
		""";

		var ag = data.Get<ExpandoObject>("Agent");
		var dtable = TableTypeBuilder.BuildDataTable(table, ag);

		var dm = await _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.Add(new SqlParameter("@Agent", SqlDbType.Structured) { TypeName = "cat.[Agent.TableType]", Value = dtable });
		});
		return dm.Root;
	}

	private Task<IDataModel> LoadIndexModelAsync(RuntimeTable table)
	{
		var sqlString = """
			select top(20) [Agents!TAgent!Array] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, a.Memo, 
				a.Code, a.FullName
			from cat.Agents a where a.TenantId = @TenantId and a.IsFolder = 0 and a.Id <> 0
			order by a.Id;
		""";

		return _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddBigInt("@Id", null);
		});
	}

	private Task<IDataModel> LoadPlainModelAsync(IPlatformUrl platformUrl, RuntimeTable table)
	{
		var sqlString = """
			select [Agent!TAgent!Object] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, a.Memo,
				a.FullName, a.Code
			from cat.Agents a where TenantId = @TenantId and a.Id = @Id
			order by a.Id;
		""";

		return _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddString("@Id", platformUrl.Id);
		});
	}
}

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
{	public Task<IDataModel> LoadModelAsync(IPlatformUrl platformUrl, IModelView view, RuntimeTable table)
	{
		return view.IsIndex ? LoadIndexModelAsync(platformUrl, table) : LoadPlainModelAsync(platformUrl, table);
	}
	private void AddDefaultParameters(DbParameterCollection prms)
	{
		prms.AddBigInt("@UserId", _currentUser.Identity.Id);
	}

	public async Task<ExpandoObject> SaveAsync(RuntimeTable table, ExpandoObject data)
	{
		var sqlString = """

		declare @rtable table(Id bigint);
		declare @id bigint;
		merge cat.Agents as t
		using @Agent as s
		on t.Id = s.Id
		when matched then update set
			t.[Name] = s.[Name],
			t.Memo = s.Memo 
		output inserted.Id into @rtable(Id);
		select top(1) @id = Id from @rtable;

		select [Agent!TAgent!Object] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, a.Memo
		from cat.Agents a where Id = @id;
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

	private Task<IDataModel> LoadIndexModelAsync(IPlatformUrl platformUrl, RuntimeTable table)
	{
		var sqlString = $"""
			set nocount on;
			set transaction isolation level read uncommitted;

			set @Order = lower(@Order);
			set @Dir = lower(@Dir);

			declare @tmp table(Id bigint, rowno int identity(1, 1), rowcnt int);
			insert into @tmp(Id, rowcnt)
			select Id, count(*) over() 
			from {table.Schema}.[{table.Name}] a
			order by
				case when @Dir = N'asc' then
					case @Order
						when N'name' then a.Name
					end
				end asc,
				case when @Dir = N'desc' then
					case @Order
						when N'name' then a.Name
					end
				end desc,
				Id
			offset @Offset rows fetch next @PageSize rows only option (recompile);

			select [{table.Name}!TAgent!Array] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, a.Memo,
				a.Code, a.FullName,
				[!!RowCount]  = t.rowcnt
			from {table.Schema}.[{table.Name}] a inner join @tmp t on a.Id = t.Id
			order by t.[rowno];

			-- system data
			select [!$System!] = null,
				[!{table.Name}!PageSize] = @PageSize,  [!{table.Name}!Offset] = @Offset,
				[!{table.Name}!SortOrder] = @Order,  [!{table.Name}!SortDir] = @Dir,
				[!{table.Name}.Fragment!Filter] = @Fragment

		""";

		var qry = platformUrl.Query;
		Int32 offset = 0;
		Int32 pageSize = 20;
		if (qry != null)
		{
			if (qry.HasProperty("Offset"))
				offset = Int32.Parse(qry.Get<String>("Offset") ?? "0");
			if (qry.HasProperty("PageSize"))
				pageSize = Int32.Parse(qry.Get<String>("PageSize") ?? "20");
		}
		return _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddBigInt("@Id", null);
			dbprms.AddInt("@Offset", offset);
			dbprms.AddInt("@PageSize", pageSize);
			dbprms.AddString("@Order", "id");
			dbprms.AddString("@Dir", "asc");
			dbprms.AddString("@Fragment", null);
		});
	}

	private Task<IDataModel> LoadPlainModelAsync(IPlatformUrl platformUrl, RuntimeTable table)
	{
		var sqlString = """
			select [Agent!TAgent!Object] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, a.Memo,
				a.FullName, a.Code
			from cat.Agents a where a.Id = @Id
			order by a.Id;
		""";

		return _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddString("@Id", platformUrl.Id);
		});
	}
}

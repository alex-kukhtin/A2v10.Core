// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;
using System.Dynamic;
using System.Threading.Tasks;
using System.Data.Common;
using System.Linq;

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
		var sqlString = $"""

		declare @rtable table(Id bigint);
		declare @Id bigint;
		merge {table.SqlTableName()} as t
		using @{table.ItemName()} as s
		on t.Id = s.Id
		when matched then update set
			t.[Name] = s.[Name],
			{String.Join(' ', table.Fields.Select(f => $"t.{f.Name} = s.{f.Name},"))}
			t.Memo = s.Memo
		when not matched then insert 
			(Name, Memo) values
			(s.Name, s.Memo)
		output inserted.Id into @rtable(Id);

		select top(1) @Id = Id from @rtable;

		{GetPlainModelSql(table)}
		""";

		var ag = data.Get<ExpandoObject>(table.ItemName());
		var dtable = TableTypeBuilder.BuildDataTable(table, ag);

		var dm = await _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.Add(new SqlParameter($"@{table.ItemName()}", SqlDbType.Structured) { TypeName = table.TableTypeName(), Value = dtable });
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
			declare @fr nvarchar(255) = N'%' + @Fragment + N'%';

			declare @tmp table(Id bigint, rowno int identity(1, 1), rowcnt int);
			insert into @tmp(Id, rowcnt)
			select Id, count(*) over() 
			from {table.SqlTableName()} a
			where a.Void = 0 and (@fr is null or a.Name like @fr or a.Memo like @fr)
			order by
				case when @Dir = N'asc' then
					case @Order
						when N'id' then a.Id
					end
				end asc,
				case when @Dir = N'asc' then
					case @Order
						when N'name' then a.Name
						when N'memo' then a.Memo
					end
				end asc,
				case when @Dir = N'desc' then
					case @Order
						when N'id' then a.Id
					end
				end desc,
				case when @Dir = N'desc' then
					case @Order
						when N'name' then a.Name
						when N'memo' then a.Memo
					end
				end desc,
						Id
			offset @Offset rows fetch next @PageSize rows only option (recompile);

			select [{table.Name}!{table.TypeName()}!Array] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, a.Memo,
				{String.Join(' ', table.Fields.Select(f => $"a.[{f.Name}],"))}
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
			dbprms.AddString("@Order", qry?.Get<String>("Order") ?? "name");
			dbprms.AddString("@Dir", qry?.Get<String>("Dir") ?? "asc");
			dbprms.AddString("@Fragment", qry?.Get<String>("Fragment"));
		});
	}

	private String GetPlainModelSql(RuntimeTable table)
	{
		return $"""
		select [{table.ItemName()}!{table.TypeName()}!Object] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, 
			{String.Join(' ', table.Fields.Select(f => $"{f.SqlField("a", table)},"))}
			a.Memo
		from {table.SqlTableName()} a where a.Id = @Id;

		-- TODO: Maps
		declare @Unit bigint;
		select @Unit = a.[Unit] from cat.[Items] a where a.Id = @Id;

		select [!TUnit!Map] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name
		from cat.Units a where a.Id = @Unit;
		""";
	}
	private Task<IDataModel> LoadPlainModelAsync(IPlatformUrl platformUrl, RuntimeTable table)
	{
		var sqlString = GetPlainModelSql(table);

		return _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddString("@Id", platformUrl.Id);
		});
	}

	public Task<IDataModel> ExecuteCommandAsync(String command, RuntimeTable table, ExpandoObject prms) 
	{
		// command === [dbo].[Fetch]
		var text = prms.Get<String>("Text");
		// TODO: Create SQL for table
		var sqlString = $"""
		declare @fr nvarchar(255) = N'%' + @Text + N'%';
		select [Units!TUnit!Array] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, 
			a.Short,
			a.Memo
		from cat.[Units] a where a.Void = 0 and (@Text is null or a.Name like @fr 
			or a.Short like @fr
			or a.Memo like @fr);
		""";
		return _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddString("@Text", text);
		});
	}
}

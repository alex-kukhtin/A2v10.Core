// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;
using System.Dynamic;
using System.Threading.Tasks;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using Microsoft.Data.SqlClient;

using A2v10.Data.Core;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.AppRuntimeBuilder;

internal class SqlModelProcessor(ICurrentUser _currentUser, IDbContext _dbContext)
{	public Task<IDataModel> LoadModelAsync(IPlatformUrl platformUrl, IModelView view, EndpointDescriptor endpoint)
	{
		return view.IsIndex ? LoadIndexModelAsync(platformUrl, endpoint.GetIndexUI()) : LoadPlainModelAsync(platformUrl, endpoint.BaseTable);
	}
	private void AddDefaultParameters(DbParameterCollection prms)
	{
		prms.AddBigInt("@UserId", _currentUser.Identity.Id);
	}

    private String MapsData(IEnumerable<RuntimeField> refFields, RuntimeTable table, String tmpTable)
    {
        String SelectFieldsMap(RuntimeTable? table, String alias)
        {
            if (table == null)
                return String.Empty;
            return String.Join(',', table.RealFieldsMap().Select(f => f.SelectSqlField(alias, table)));
        }

        if (!refFields.Any())
            return String.Empty;
        var sb = new StringBuilder();
        foreach (var rf in refFields)
        {
            var rtable = table.FindTable(rf.Ref!);
            sb.AppendLine($"""
				with TM as (select [{rf.MapName()}] from {tmpTable} group by [{rf.MapName()}])
				select [!{rtable.TypeName()}!Map] = null, 
					{SelectFieldsMap(rtable, "m")}
				from TM inner join {rtable.SqlTableName()} m on TM.[{rf.MapName()}] = m.Id;
				""");
			sb.AppendLine();
        }
        return sb.ToString();
    }


    public async Task<ExpandoObject> SaveAsync(RuntimeTable table, ExpandoObject data)
	{
		String MergeDetails()
		{
			if (table.Details == null || table.Details.Count == 0)
				return String.Empty;
			var sb = new StringBuilder();
			foreach (var details in table.Details)
			{
				var updateFields = details.RealFields().Where(f => f.Type != FieldType.Parent && f.Name != "Id");
				var parentField = details.RealFields().FirstOrDefault(f => f.Type == FieldType.Parent)
					?? throw new InvalidOperationException("Parent field not found");
				sb.AppendLine($"""
				merge {details.SqlTableName()} as t
				using @{details.Name} as s
				on t.Id = s.Id
				when matched then update set
					{String.Join(',', updateFields.Select(f => $"t.{f.Name} = s.{f.Name}"))}
				when not matched then insert 
					({parentField.Name}, {String.Join(',', updateFields.Select(f => f.Name))}) values
					(@Id, {String.Join(',', updateFields.Select(f => $"s.{f.Name}"))})
				when not matched by source and t.[{parentField.Name}] = @Id then delete;
				""");
				sb.AppendLine();
			}
			return sb.ToString();
		}

		var updateFields = table.RealFields().Where(f => f.Name != "Id");
		var sqlString = $"""

		set nocount on;
		set transaction isolation level read committed;
		set xact_abort on;

		declare @rtable table(Id bigint);
		declare @Id bigint;

		begin tran;
		merge {table.SqlTableName()} as t
		using @{table.ItemName()} as s
		on t.Id = s.Id
		when matched then update set
			{String.Join(',', updateFields.Select(f => $"t.{f.Name} = s.{f.Name}"))}
		when not matched then insert 
			({String.Join(',', updateFields.Select(f => f.Name))}) values
			({String.Join(',', updateFields.Select(f => $"s.{f.Name}"))})
		output inserted.Id into @rtable(Id);

		select top(1) @Id = Id from @rtable;

		{MergeDetails()}

		commit tran;

		{GetPlainModelSql(table)}
		""";

		var item = data.Get<ExpandoObject>(table.ItemName());
		var dtable = TableTypeBuilder.BuildDataTable(table, item);

		var dm = await _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.Add(new SqlParameter($"@{table.ItemName()}", SqlDbType.Structured) { TypeName = table.TableTypeName(), Value = dtable });
			if (table.Details != null && item != null)
				foreach (var details in table.Details)
				{
					var rows = item.Get<List<Object>>(details.Name);
                    var dtable = TableTypeBuilder.BuildDataTable(details, rows);
                    dbprms.Add(new SqlParameter($"@{details.Name}", SqlDbType.Structured) { TypeName = details.DetailsTableTypeName(table), Value = dtable });
				}
		});
		return dm.Root;
	}

	private Task<IDataModel> LoadIndexModelAsync(IPlatformUrl platformUrl, IndexUiElement indexUi)
	{
		var table = indexUi.Endpoint?.BaseTable
			?? throw new InvalidOperationException("Endpoint or BaseTable is null");

        // TODO: search fields from UI, sort non string fields

        var refFields = table.Fields.Where(f => f.Ref != null);

        String RefTableFields()
		{
			return String.Join(' ', refFields.Select(rf => $"[{rf.MapName()}] bigint,"));
		}

        String RefInsertFields()
        {
            return String.Join(' ', refFields.Select(rf => $"[{rf.MapName()}],"));
        }

		String SelectFieldsAll(RuntimeTable table, String alias) {
			return String.Join(' ', table.RealFields().Select(f => $"{f.SelectSqlField(alias, table)},"));
		}

		String SortPredicate(Func<RuntimeField, Boolean> predicate, String alias)
		{
			var strFields = table.RealFields().Where(predicate);
			if (!strFields.Any())
				return String.Empty;
			var fields = String.Join(' ', strFields.Select(f => $"when N'{f.Name.ToLowerInvariant()}' then {alias}.[{f.Name}] "));
			return $"""
			case when @Dir = N'asc' then
				case @Order
					{fields}
				end
			end asc,
			case when @Dir = N'desc' then
				case @Order
					{fields}
				end
			end desc,			
			""";
		}

        var sqlString = $"""
		set nocount on;
		set transaction isolation level read uncommitted;

		set @Order = lower(@Order);
		set @Dir = lower(@Dir);
		declare @fr nvarchar(255) = N'%' + @Fragment + N'%';

		declare @tmp table(Id bigint, rowno int identity(1, 1), {RefTableFields()} rowcnt int);
		insert into @tmp(Id, {RefInsertFields()} rowcnt)
		select a.Id, {RefInsertFields()} count(*) over() 
		from {table.SqlTableName()} a
		where a.Void = 0 and (@fr is null 
			{String.Join(' ', table.SearchField().Select(f => $"or a.{f.Name} like @fr"))}
			or a.Memo like @fr)
		order by
			{SortPredicate(f => f.Type == FieldType.String, "a")}
			{SortPredicate(f => f.Type == FieldType.Id || f.Type == FieldType.Money || f.Type == FieldType.Float, "a")}
			{SortPredicate(f => f.Type == FieldType.Date || f.Type == FieldType.DateTime, "a")}
			a.[Id]
		offset @Offset rows fetch next @PageSize rows only option (recompile);

		select [{table.Name}!{table.TypeName()}!Array] = null,
			{SelectFieldsAll(table, "a")}
			[!!RowCount]  = t.rowcnt
		from {table.SqlTableName()} a inner join @tmp t on a.Id = t.Id
		order by t.[rowno];

		{MapsData(refFields, table, "@tmp")}

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
			// TODO: Default order
			dbprms.AddString("@Order", qry?.Get<String>("Order") ?? "name");
			dbprms.AddString("@Dir", qry?.Get<String>("Dir") ?? "asc");
			dbprms.AddString("@Fragment", qry?.Get<String>("Fragment"));
		});
	}

	private String GetPlainModelSql(RuntimeTable table)
    {
		var refFieldsMap = table.Fields.Where(f => f.Ref != null);

		// TODO: Нужно использовать TableName а не FieldName
		IEnumerable<RuntimeField> DetailsFieldMap()
		{
			if (table.Details != null)
				foreach (var detailsTable in table.Details)
				{
					foreach (var f in detailsTable.Fields.Where(f => f.Ref != null))
					{
						yield return f;
						var rt = detailsTable.FindTable(f.Ref!);
						foreach (var f2 in rt.Fields.Where(f => f.Ref != null))
							yield return f2;
					}
				}
		}

		var detailsFieldMap = DetailsFieldMap();	
		var fullFieldsMap = refFieldsMap.Union(detailsFieldMap);

		String DeclareMap()
		{
            if (!fullFieldsMap.Any())
                return String.Empty;
			return $"""
			declare @map table(Id bigint, {String.Join(',', fullFieldsMap.Select(f => $"{f.MapName()} bigint"))});
			""";
        }

        String InsertMapMain()
		{
			if (!refFieldsMap.Any())
				return String.Empty;
			return $"""
			insert into @map(Id, {String.Join(',', refFieldsMap.Select(f => f.MapName()))})
			select a.Id, {String.Join(',', refFieldsMap.Select(f => $"a.{f.Name}"))} 
			from {table.SqlTableName()} a where a.Id = @Id;
			""";
		}

        String InsertMapDetails()
		{
			if (!detailsFieldMap.Any())
				return String.Empty;
			if (table.Details == null)
				return String.Empty;
			var sb = new StringBuilder();
			foreach (var details in table.Details) {
				var fieldMap = details.Fields.Where(f => f.Ref != null);
                var parentField = details.RealFields().FirstOrDefault(f => f.Type == FieldType.Parent)
                    ?? throw new InvalidOperationException("Parent field not found");
                sb.AppendLine($"""
				insert into @map({String.Join(',', fieldMap.Select(f => f.MapName()))})
				select {String.Join(',', fieldMap.Select(f => $"a.[{f.Name}]"))}
				from {details.SqlTableName()} a where a.[{parentField.Name}] = @Id;
				""");
				sb.AppendLine();
            }
            return sb.ToString();
		}

		String DetailsFields()
		{
			if (table.Details == null)
				return String.Empty;
			var details = String.Join(',', table.Details.Select(f => $"[{f.Name}!{f.TypeName()}!Array] = null"));
			if (String.IsNullOrEmpty(details))
				return String.Empty;
			return "," + details;
		} 

		String DetailsTable()
		{
            if (table.Details == null)	
				return String.Empty;
			var sb = new StringBuilder();
			foreach (var details in table.Details)
			{
				var fields = String.Join(',', details.RealFields().Select(f => f.SelectSqlField("d", details)));
                var parentField = details.RealFields().FirstOrDefault(f => f.Type == FieldType.Parent)
                    ?? throw new InvalidOperationException("Parent field not found");
                sb.AppendLine($"""
				select [!{details.TypeName()}!Array] = null,
					{fields}
				from {details.SqlTableName()} d where d.[{parentField.Name}] = @Id;
				""");
			}
			return sb.ToString();	
		}

        return $"""
		select [{table.ItemName()}!{table.TypeName()}!Object] = null, 
			{String.Join(',', table.RealFields().Select(f => $"{f.SelectSqlField("a", table)}"))}
			{DetailsFields()}
		from {table.SqlTableName()} a where a.Id = @Id;

		{DeclareMap()}
		{DetailsTable()}
		{InsertMapMain()}

		{InsertMapDetails()}
		{MapsData(fullFieldsMap, table, "@map")}
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

	private Task<IDataModel> ExecuteFetch(RuntimeTable table, ExpandoObject prms)
	{
		var text = prms.Get<String>("Text");
		var sqlString = $"""
			declare @fr nvarchar(255) = N'%' + @Text + N'%';
			select [{table.Name}!{table.TypeName()}!Array] = null, [Id!!Id] = a.Id, [Name!!Name] = a.[Name], 
				{String.Join(' ', table.SearchField().Select(f => $"a.{f.Name},"))}
				a.Memo
			from {table.SqlTableName()} a where a.Void = 0 and (@Text is null 
				or a.[Name] like @fr 
				{String.Join(' ', table.SearchField().Select(f => $"or a.{f.Name} like @fr"))}
				or a.Memo like @fr);
			""";
		return _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddString("@Text", text);
		});
	}
	public Task<IDataModel> ExecuteCommandAsync(String command, RuntimeTable table, ExpandoObject prms) 
	{
		// command === [dbo].[Fetch]
		if (command == "[dbo].[Fetch]")
		{
			return ExecuteFetch(table, prms);
		}
		throw new NotImplementedException($"Command {command} yet not implemented");
	}
}

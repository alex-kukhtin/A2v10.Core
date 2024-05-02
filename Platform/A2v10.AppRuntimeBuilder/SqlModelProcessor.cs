// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;
using System.Dynamic;
using System.Threading.Tasks;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

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

    private static String MapsData(IEnumerable<RuntimeField> refFields, RuntimeTable table, String tmpTable)
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
				with TM as (select [{rf.MapName()}] from {tmpTable} where [{rf.MapName()}] is not null group by [{rf.MapName()}])
				select [!{rtable.TypeName()}!Map] = null, 
					{SelectFieldsMap(rtable, "m")}
				from TM inner join {rtable.SqlTableName()} m on TM.[{rf.MapName()}] = m.Id;
				""");
			sb.AppendLine();
        }
        return sb.ToString();
    }


    public async Task<ExpandoObject> SaveAsync(EndpointDescriptor endpoint, ExpandoObject data)
	{
		var table = endpoint.BaseTable;


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

		var updateFields = table.RealFields().Where(f => f.Name != "Id" && !endpoint.IsParameter(f));

		String? paramFields = null;
		String? paramValues = null;

		if (endpoint.Parameters != null && endpoint.Parameters.Count > 0)
		{
			paramFields = $"{String.Join(",", endpoint.Parameters.Select(p => p.Key))}, ";
			paramValues = $"{String.Join(",", endpoint.Parameters.Select(p => $"N'{p.Value}'"))}, ";
		}

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
			({paramFields}{String.Join(',', updateFields.Select(f => f.Name))}) values
			({paramValues}{String.Join(',', updateFields.Select(f => $"s.{f.Name}"))})
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

		var qry = platformUrl.Query;
		Int32 offset = 0;
		Int32 pageSize = 20;
		String? fragment = null;
		String order = indexUi.SortOrder();
		String dir = indexUi.SortDirection();
		if (qry != null)
		{
			if (qry.HasProperty("Offset"))
				offset = Int32.Parse(qry.Get<String>("Offset") ?? "0");
			if (qry.HasProperty("PageSize"))
				pageSize = Int32.Parse(qry.Get<String>("PageSize") ?? "20");
			fragment = qry?.Get<String>("Fragment");
			order = qry?.Get<String>("Order") ?? indexUi.SortOrder();
			dir = qry?.Get<String>("Dir")?.ToLowerInvariant() ?? indexUi.SortDirection();
		}

		var refFields = table.Fields.Where(f => f.Ref != null);

		var filters = indexUi.Fields.Where(f => f.Filter);
		var hasPeriod = filters.Any(f => f.IsPeriod());
		filters = filters.Where(f => !f.IsPeriod()); // exclude period

        String RefTableFields() =>
			String.Join(' ', refFields.Select(rf => $"[{rf.MapName()}] bigint,"));

        String RefInsertFields() =>
            String.Join(' ', refFields.Select(rf => $"[{rf.MapName()}],"));

		String SelectFieldsAll(RuntimeTable table, String alias) =>
			String.Join(' ', table.RealFields().Select(f => $"{f.SelectSqlField(alias, table)},"));

		String PeriodWhere() => hasPeriod ? "and a.[Date] >= @From and a.[Date] <= @To " : String.Empty;
		String PeriodDefault() => hasPeriod ?
			"""
			set @From = isnull(@From, getdate());
			set @To = isnull(@To, @From);
			""" : String.Empty;
		String PeriodSystem() => hasPeriod ? $",[!{table.Name}.Period.From!Filter] = @From, [!{table.Name}.Period.To!Filter] = @To" : String.Empty;

		String FilterToMap()
		{
			if (!filters.Any())
				return String.Empty;
			return $""""
			insert into @tmp({String.Join(',', filters.Select(f => $"{f.BaseField?.MapName()}"))}) values
			({String.Join(',', filters.Select(f => $"@{f.Name}"))});
			"""";
		}

		String ParametersCondition()
		{
            if (indexUi.Endpoint.Parameters == null)
				return String.Empty;
			var str = String.Join(" and ", indexUi.Endpoint.Parameters.Select(p => $"a.[{p.Key}] = N'{p.Value}'"));
			if (str.Length > 0)
				return $"and {str}";
			return String.Empty;
        }

		String WhereCondition()
		{
			if (!filters.Any() && String.IsNullOrEmpty(fragment))
				return String.Empty;
			List<String> list = [];

			if (fragment != null)
			{
				var sf = indexUi.Fields.Where(f => f.Search == SearchType.Like).Select(f => $"a.{f.Name} like @fr");
				list.Add($" ({String.Join(" or ", sf)}) ");
			}
			foreach (var f in filters) {
				var dat = qry?.Get<Object>(f.Name);
				if (dat == null)
					continue;
				list.Add($"a.[{f.Name}] = @{f.Name}");
			}
			if (list.Count == 0)
				return String.Empty;
			return $"and {String.Join(" and ", list)}";
		}

		var sqlString = $"""
		set nocount on;
		set transaction isolation level read uncommitted;

		set @Order = lower(@Order);
		set @Dir = lower(@Dir);

		declare @fr nvarchar(255) = N'%' + @Fragment + N'%';
	
		{PeriodDefault()}
		
		declare @tmp table(Id bigint, rowno int identity(1, 1), {RefTableFields()} rowcnt int);
		insert into @tmp(Id, {RefInsertFields()} rowcnt)
		select a.Id, {RefInsertFields()} count(*) over() 
		from {table.SqlTableName()} a
		where a.Void = 0 {ParametersCondition()} {PeriodWhere()} {WhereCondition()}
		order by
			a.[{table.RealFields().FirstOrDefault(f => f.Name.Equals(order, StringComparison.OrdinalIgnoreCase))?.Name}] {dir}
		offset @Offset rows fetch next @PageSize rows only option (recompile);

		select [{table.Name}!{table.TypeName()}!Array] = null,
			{SelectFieldsAll(table, "a")}
			[!!RowCount]  = t.rowcnt
		from {table.SqlTableName()} a inner join @tmp t on a.Id = t.Id
		order by t.[rowno];

		{FilterToMap()}
		{MapsData(refFields, table, "@tmp")}

		-- system data
		select [!$System!] = null,
			[!{table.Name}!PageSize] = @PageSize,  [!{table.Name}!Offset] = @Offset,
			[!{table.Name}!SortOrder] = @Order,  [!{table.Name}!SortDir] = @Dir,
			[!{table.Name}.Fragment!Filter] = @Fragment
			{String.Join(' ', filters.Select(f => $",[!{table.Name}.{f.Name}.{f.RefTable?.TypeName()}.RefId!Filter] = @{f.Name}"))}
			{PeriodSystem()}

		""";

		return _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddBigInt("@Id", null);
			dbprms.AddInt("@Offset", offset);
			dbprms.AddInt("@PageSize", pageSize);
			dbprms.AddString("@Order", order);
			dbprms.AddString("@Dir", dir);
			dbprms.AddString("@Fragment", fragment);
			if (hasPeriod)
			{
				dbprms.Add(new SqlParameter("@From", SqlDbType.Date) { Value = GetDateParameter(qry, "From") });
				dbprms.Add(new SqlParameter("@To", SqlDbType.Date) { Value = GetDateParameter(qry, "To") });
			};
			foreach (var f in filters)
			{
				var val = qry?.Get<String>(f.Name);
				dbprms.AddBigInt($"@{f.Name}", String.IsNullOrEmpty(val) ? null : Int64.Parse(val));
			}
		});
	}

	// TODO: Перенести в Data.Core.DbParamsExtension
	Object GetDateParameter(ExpandoObject? eo, String name)
	{
		var val = eo?.Get<Object>(name);
		if (val == null)
			return DBNull.Value;
		if (val is DateTime dt)
			return dt;
		else if (val is String strVal)
			return DateTime.ParseExact(strVal, "yyyyMMdd", CultureInfo.InvariantCulture);
		throw new InvalidExpressionException($"Invalid Date Parameter value: {val}");
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

	private Task<IDataModel> ExecuteApply(EndpointDescriptor endpoint, ExpandoObject prms)
	{
		throw new NotImplementedException("Execute APPLY");
	}

	private Task<IDataModel> ExecuteUnApply(EndpointDescriptor endpoint, ExpandoObject prms)
	{
		throw new NotImplementedException("Execute APPLY");
	}

	private Task<IDataModel> ExecuteFetch(EndpointDescriptor endpoint, ExpandoObject prms)
	{
		var table = endpoint.BaseTable;
		var indexUi = endpoint.GetBrowseUI();

		var text = prms.Get<String>("Text");

		String WhereCondition()
		{
			String? cond = null;
			if (text != null)
				cond = String.Join(" or ", indexUi.Fields.Where(f => f.Search == SearchType.Like).Select(f => $"a.{f.Name} like @fr"));
			return cond != null ? $" and ({cond})" : String.Empty;
		}

		var sqlString = $"""
			declare @fr nvarchar(255) = N'%' + @Text + N'%';
			select [{table.Name}!{table.TypeName()}!Array] = null,
				{String.Join(',', table.RealFields().Select(f => $"{f.SelectSqlField("a", table)}"))}
			from {table.SqlTableName()} a where a.Void = 0 {WhereCondition()};
			""";
		return _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddString("@Text", text);
		});
	}
	public Task<IDataModel> ExecuteCommandAsync(String command, EndpointDescriptor endpoint, ExpandoObject prms) 
	{
		return command switch
		{
			"[dbo].[Fetch]" => ExecuteFetch(endpoint, prms),
			"[dbo].[Apply]" => ExecuteApply(endpoint, prms),
			"[dbo].[Unapply]" => ExecuteUnApply(endpoint, prms),
			_ => throw new NotImplementedException($"Command {command} yet not implemented")
		};
	}
}

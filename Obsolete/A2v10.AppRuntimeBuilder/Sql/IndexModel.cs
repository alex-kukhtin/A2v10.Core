// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;

using Microsoft.Data.SqlClient;

using A2v10.Data.Interfaces;
using A2v10.Data.Core.Extensions;
using A2v10.Infrastructure;
using A2v10.AppRuntimeBuilder.Sql;

namespace A2v10.AppRuntimeBuilder;

internal partial class SqlModelProcessor
{
    private static IEnumerable<String> VoidParam => ["a.Void = 0"];

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

		var filters = indexUi.Fields.Where(f => f.Filter && !f.Name.Contains('.'));
		var hasPeriod = filters.Any(f => f.IsPeriod());
		filters = filters.Where(f => !f.IsPeriod()); // exclude period

		String RefTableFields() =>
			String.Join(' ', refFields.Select(rf => $"[{rf.Name}] bigint,"));

		String RefInsertFields() =>
			String.Join(' ', refFields.Select(rf => $"[{rf.Name}],"));

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
			insert into @tmp({String.Join(',', filters.Select(f => $"[{f.Name}]"))}) values
			({String.Join(',', filters.Select(f => $"@{f.Name}"))});
			"""";
		}

		String ParametersCondition()
		{
            var hasVoid = indexUi.Endpoint.EndpointType() == TableType.Catalog;
			if (indexUi.Endpoint.Parameters == null)
				return hasVoid ? "a.Void = 0" : String.Empty;
			var prms = indexUi.Endpoint.Parameters.Select(p => $"a.[{p.Key}] = @{p.Key}");
			var cond = hasVoid ? VoidParam.Union(prms) : prms;
            return String.Join(" and ", cond);
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
			foreach (var f in filters)
			{
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
		where {ParametersCondition()} {PeriodWhere()} {WhereCondition()}
		order by
			a.[{table.RealFields().FirstOrDefault(f => f.Name.Equals(order, StringComparison.OrdinalIgnoreCase))?.Name ?? "Id"}] {dir}
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

			if (indexUi.Endpoint.Parameters != null)
				foreach (var eprm in indexUi.Endpoint.Parameters)
					dbprms.AddString($"@{eprm.Key}", eprm.Value);

			if (hasPeriod)
			{
				dbprms.Add(new SqlParameter("@From", SqlDbType.Date) { Value = SqlHelpers.GetDateParameter(qry, "From") });
				dbprms.Add(new SqlParameter("@To", SqlDbType.Date) { Value = SqlHelpers.GetDateParameter(qry, "To") });
			};
			foreach (var f in filters)
			{
				var val = qry?.Get<String>(f.Name);
				dbprms.AddBigInt($"@{f.Name}", String.IsNullOrEmpty(val) ? null : Int64.Parse(val));
			}
		});
	}
}

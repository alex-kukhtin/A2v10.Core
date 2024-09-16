// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Dynamic;

using Microsoft.Data.SqlClient;

using A2v10.Data.Interfaces;
using A2v10.Data.Core.Extensions;
using A2v10.Infrastructure;

namespace A2v10.AppRuntimeBuilder;

internal partial class SqlModelProcessor
{
	private static String GetPlainModelSql(RuntimeTable table)
	{
		var refFieldsMap = table.Fields.Where(f => f.Ref != null);

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
			declare @map table(Id bigint, {String.Join(',', fullFieldsMap.Select(f => $"[{f.Name}] bigint"))});
			""";
		}

		String InsertMapMain()
		{
			if (!refFieldsMap.Any())
				return String.Empty;
			return $"""
			insert into @map(Id, {String.Join(',', refFieldsMap.Select(f => $"[{f.Name}]"))})
			select a.Id, {String.Join(',', refFieldsMap.Select(f => $"a.[{f.Name}]"))} 
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
			foreach (var details in table.Details)
			{
				var fieldMap = details.Fields.Where(f => f.Ref != null);
				var parentField = details.RealFields().FirstOrDefault(f => f.Type == FieldType.Parent)
					?? throw new InvalidOperationException("Parent field not found");
				sb.AppendLine($"""
				insert into @map({String.Join(',', fieldMap.Select(f => $"[{f.Name}]"))})
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

		String SystemRecordset()
		{
			if (table.Schema == "doc")
				return $"""
				select [!$System!] = null, [!!ReadOnly] = d.Done
				from {table.SqlTableName()} d where Id = @Id;
				""";
			return String.Empty;
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

		{SystemRecordset()}
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
					{String.Join(',', updateFields.Select(f => $"t.[{f.Name}] = s.[{f.Name}]"))}
				when not matched then insert 
					({parentField.Name}, {String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
					(@Id, {String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
				when not matched by source and t.[{parentField.Name}] = @Id then delete;
				""");
				sb.AppendLine();
			}
			return sb.ToString();
		}

		var updateFields = table.RealFields().Where(f => f.Name != "Id" && !endpoint.IsParameter(f) && f.Name != "Void");

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
			{String.Join(',', updateFields.Select(f => $"t.[{f.Name}] = s.[{f.Name}]"))}
		when not matched then insert 
			({paramFields}{String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
			({paramValues}{String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
		output inserted.Id into @rtable(Id);

		select top(1) @Id = Id from @rtable;

		{MergeDetails()}

		commit tran;

		{GetPlainModelSql(table)}
		""";

		var item = data.Get<ExpandoObject>(table.ItemName());
		var dtable = DataTableBuilder.BuildDataTable(table, item);

		var dm = await _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.Add(new SqlParameter($"@{table.ItemName()}", SqlDbType.Structured) { TypeName = table.TableTypeName(), Value = dtable });
			if (table.Details != null && item != null)
				foreach (var details in table.Details)
				{
					var rows = item.Get<List<Object>>(details.Name);
					var dtable = DataTableBuilder.BuildDataTable(details, rows);
					dbprms.Add(new SqlParameter($"@{details.Name}", SqlDbType.Structured) { TypeName = details.DetailsTableTypeName(table), Value = dtable });
				}
		});
		return dm.Root;
	}

}

// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using A2v10.Data.Interfaces;
using A2v10.Data.Core;
using A2v10.Data;

namespace A2v10.AppRuntimeBuilder;

internal class FieldNameComparer : IEqualityComparer<RuntimeField>
{
	public bool Equals(RuntimeField? x, RuntimeField? y)
	{
		return x?.Name == y?.Name;
	}

	public int GetHashCode([DisallowNull] RuntimeField field)
	{
		return field.GetHashCode();
	}
}
internal partial class SqlModelProcessor
{
	private static String DeleteFromJournalsSql(EndpointDescriptor endpoint)
	{
		var sb = new StringBuilder();
		foreach (var appl in endpoint.Apply)
		{
			var jrnTable = endpoint.GetTable($"Journal.{appl.Journal}")
				?? throw new InvalidOperationException($"Journal '{appl.Journal}' not found");
			var refField = jrnTable.Fields.FirstOrDefault(f => f.Type == FieldType.Reference && f.Ref == endpoint.Table)
				?? throw new InvalidOperationException($"Reference '{endpoint.Table}' not found");
			sb.AppendLine($"""
			delete from {jrnTable.SqlTableName()} where [{refField.Name}] = @Id;
			""");
		}
		return sb.ToString();	
	}
	private Task<IDataModel> ExecuteApply(EndpointDescriptor endpoint, ExpandoObject prms)
	{
		var baseTable = endpoint.BaseTable;

		IEnumerable<RuntimeField> IntersectFields(RuntimeTable jrn, RuntimeTable details)
			=> jrn.Fields.Intersect(details.Fields, new FieldNameComparer());

		String SelectFieldWithStorno(RuntimeField field, String prefix, Boolean storno)
		{
			var sign = field.CanStorno() && storno ? "-" : "";
			return $"{sign}{prefix}.[{field.Name}]";
		}

		IEnumerable<RuntimeField> FindInOutInsert(RuntimeTable jrn, RuntimeTable doc, String InOut, Boolean isInsert)
		{
			foreach (var f in jrn.Fields.Where(f => f.Ref != null))
			{
				var xf = doc.Fields.FirstOrDefault(df => f.Name + InOut == df.Name);
				if (xf != null)
					yield return isInsert ? f : xf; // SOURCE or Target
			}
		}

		String InsertToJournals()
		{
			var sb = new StringBuilder();

			String InsertIntoJounalSql(Boolean inOut, Boolean storno, RuntimeField refField, RuntimeTable jrnTable, RuntimeTable sourceTable) 
			{
				var inOutVal = inOut ? 1 : -1;
				var inOutStr = inOut ? "In" : "Out";

				var enumFieldsInsert = IntersectFields(jrnTable, sourceTable).Select(f => $"[{f.Name}]");
				var enumFieldsSelect = IntersectFields(jrnTable, sourceTable).Select(f => SelectFieldWithStorno(f, "dd", storno));
				var enumFieldsInsertDoc = IntersectFields(jrnTable, baseTable).Select(f => $"[{f.Name}]");
				var enumFieldsSelectDoc = IntersectFields(jrnTable, baseTable).Select(f => SelectFieldWithStorno(f, "d", storno));
				var enumFieldsInsertInOut = FindInOutInsert(jrnTable, baseTable, inOutStr, true).Select(f => $"[{f.Name}]");
				var enumFieldsSelectInOut = FindInOutInsert(jrnTable, baseTable, inOutStr, false).Select(f => $"[{f.Name}]");

				var ddFieldsInsert = String.Join(", ", enumFieldsInsert.Union(enumFieldsInsertDoc).Union(enumFieldsInsertInOut));
				var ddFieldsSelect = String.Join(", ", enumFieldsSelect.Union(enumFieldsSelectDoc).Union(enumFieldsSelectInOut));
				return $"""
				insert into {jrnTable.SqlTableName()} ([{refField.Name}], [Date], [InOut], {ddFieldsInsert})
				select [{refField.Name}], d.[Date], {inOutVal}, {ddFieldsSelect}
				from {sourceTable.SqlTableName()} dd
					inner join {baseTable.SqlTableName()} d on dd.[{refField.Name}] = d.Id
				where dd.[{refField.Name}] = @Id;
				""";
			}

			foreach (var appl in endpoint.Apply)
			{
				var jrnTable = endpoint.GetTable($"Journal.{appl.Journal}")
					?? throw new InvalidOperationException($"Journal '{appl.Journal}' not found");
				var refField = jrnTable.Fields.FirstOrDefault(f => f.Type == FieldType.Reference && f.Ref == endpoint.Table)
					?? throw new InvalidOperationException($"Reference '{endpoint.Table}' not found");

				if (appl.Source.Contains('.'))
				{
					var srcArr = appl.Source.Split('.');
					var sourceTable = baseTable.Details?.FirstOrDefault(f => f.Name == srcArr[1])
						?? throw new InvalidOperationException($"Source'{appl.Source}' not found");
					if (appl.In)
						sb.AppendLine(InsertIntoJounalSql(true, appl.Storno, refField, jrnTable, sourceTable));
					if (appl.Out)
						sb.AppendLine(InsertIntoJounalSql(false, appl.Storno, refField, jrnTable, sourceTable));
				}
				else
				{
					throw new NotImplementedException("Apply whole Document yet not implemented");
				}
			}
			return sb.ToString();
		}

		var sqlApplyString = $"""
		set nocount on;
		set transaction isolation level read committed;
		set xact_abort on;

		begin tran;
		{DeleteFromJournalsSql(endpoint)}
		{InsertToJournals()}
		update {baseTable.SqlTableName()} set Done = 1 where Id = @Id;
		commit tran;

		""";

		return _dbContext.LoadModelSqlAsync(null, sqlApplyString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddBigInt("@Id", prms.Get<Int64>("Id"));
		});
	}

	private Task<IDataModel> ExecuteUnApply(EndpointDescriptor endpoint, ExpandoObject prms)
	{
		var table = endpoint.BaseTable;
		var sqlApplyString = $"""
		set nocount on;
		set transaction isolation level read committed;
		set xact_abort on;

		begin tran;
		{DeleteFromJournalsSql(endpoint)}
		update {table.SqlTableName()} set Done = 0 where Id = @Id;
		commit tran;

		""";

		return _dbContext.LoadModelSqlAsync(null, sqlApplyString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddBigInt("@Id", prms.Get<Int64>("Id"));
		});
	}
}

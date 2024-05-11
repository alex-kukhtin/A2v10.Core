// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;
using System.Text;
using System.Linq;
using System.Collections.Generic;

using A2v10.Data.Interfaces;
using A2v10.Data.Core;
using A2v10.Data;
using System.Diagnostics.CodeAnalysis;

namespace A2v10.AppRuntimeBuilder;

internal class FieldNameComparer : IEqualityComparer<RuntimeField>
{
	public bool Equals(RuntimeField? x, RuntimeField? y)
	{
		return x?.Name == y?.Name;
	}

	public int GetHashCode([DisallowNull] RuntimeField obj)
	{
		return obj.GetHashCode();
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

		IEnumerable<String> IntersectFields(RuntimeTable jrn, RuntimeTable details, String prefix)
			=> jrn.Fields.Intersect(details.Fields, new FieldNameComparer()).Select(f => $"{prefix}[{f.Name}]");

		String InsertToJournals()
		{
			var sb = new StringBuilder();
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

					if (appl.In && appl.Out)
						throw new NotImplementedException("Apply In/Out yet not implemented");
					else
					{
						var inOutVal = appl.In ? 1 : appl.Out ? -1 : 0;
						sb.AppendLine($"""
						insert into {jrnTable.SqlTableName()} ([{refField.Name}], [Date], [InOut], {String.Join(", ", IntersectFields(jrnTable, sourceTable, String.Empty))})
						select [{refField.Name}], d.[Date], {inOutVal}, {String.Join(", ", IntersectFields(jrnTable, sourceTable, "dd."))} 
						from {sourceTable.SqlTableName()} dd
							inner join {baseTable.SqlTableName()} d on dd.[{refField.Name}] = d.Id
						where dd.[{refField.Name}] = @Id;
						""");
					}
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

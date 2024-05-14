// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;
using System.Dynamic;
using System.Threading.Tasks;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using A2v10.Data.Core;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.AppRuntimeBuilder;

internal partial class SqlModelProcessor(ICurrentUser _currentUser, IDbContext _dbContext)
{	
	public Task<IDataModel> LoadModelAsync(IPlatformUrl platformUrl, IModelView view, EndpointDescriptor endpoint)
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
				with TM as (select [{rf.Name}] from {tmpTable} where [{rf.Name}] is not null group by [{rf.Name}])
				select [!{rtable.TypeName()}!Map] = null, 
					{SelectFieldsMap(rtable, "m")}
				from TM inner join {rtable.SqlTableName()} m on TM.[{rf.Name}] = m.Id;
				""");
			sb.AppendLine();
        }
        return sb.ToString();
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

	public Task<IDataModel> DbRemoveAsync(String? propName, EndpointDescriptor endpoint, ExpandoObject prms)
	{
		String command = $"{endpoint.BaseTable.Schema}.{endpoint.BaseTable.Name}";
		var refs = endpoint.AllReferences();
		var exists = refs.Select(f => $"""
		exists(select * from {f.Table.SqlTableName()} where [{f.Name}] = @Id)
		""");

		var sqlCheck = "";
		if (exists.Any())
			sqlCheck = $"""
			if {String.Join("\n or ", exists)}
				throw 60000, N'UI:@[Error.Delete.Used]', 0;
			""";

		var sqlString = $"""
			{sqlCheck}

			update {endpoint.BaseTable.SqlTableName()} set Void = 1 where Id = @Id;
			""";
		return _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			AddDefaultParameters(dbprms);
			dbprms.AddBigInt("@Id", prms.Get<Int64>("Id"));
		});
	}
}

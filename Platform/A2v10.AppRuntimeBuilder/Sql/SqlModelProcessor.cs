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

using A2v10.Data.Core;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.AppRuntimeBuilder;

internal partial class SqlModelProcessor(ICurrentUser _currentUser, IDbContext _dbContext)
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


	// TODO: Перенести в Data.Core.DbParamsExtension
	static Object GetDateParameter(ExpandoObject? eo, String name)
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

	public Task<IDataModel> DbRemoveAsync(String? propName, EndpointDescriptor endpoint, ExpandoObject prms)
	{
		String command = $"{endpoint.BaseTable.Schema}.{endpoint.BaseTable.Name}";
		// var refs = endpoint.BaseTable.AllReferencs();
		throw new NotImplementedException($"Delete from {command} yet not implemented");
	}
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Dynamic;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Data.Core.Extensions;

namespace A2v10.Cli;

internal class ColumnsCommand(IServiceProvider services)
{
    private readonly IDbContext _dbContext = services.GetRequiredService<IDbContext>();
    internal Command Build()
    {
        var cmd = new Command("table-columns", "List table columns");
        var tableArg = new Argument<String>("table")
        {
            Description = "Table name",
        };
        cmd.Arguments.Add(tableArg);
        cmd.Options.Add(new DescribeOption());

        cmd.SetAction(r => JsonResult.Try(() => ColumnList(r.GetValue(tableArg)!)));
        return cmd;
    }

    private async Task<Object> ColumnList(String table)
    {
        var sqlString = """
        set nocount on;
        set transaction isolation level read uncommitted;

        select [Columns!TColumn!Array] = null,  [Name] = sc.[name], 
        	[Type] = case when tp.[Name] = 'nvarchar' then tp.[name] + '(' + cast(sc.max_length / 2 as sysname) + ')' else tp.[name] end,
            Ref = rs.[name] + '.[' + rt.[name] + ']'-- ref_column = rc.[name]
        from sys.columns sc
            inner join sys.types tp on tp.user_type_id = sc.user_type_id
            left join sys.foreign_key_columns fkc on fkc.parent_object_id = sc.object_id and fkc.parent_column_id = sc.column_id
            left join sys.foreign_keys fk on fk.object_id = fkc.constraint_object_id
            left join sys.objects rt on rt.object_id = fk.referenced_object_id
            left join sys.schemas rs on rs.schema_id = rt.schema_id
            left join sys.columns rc on rc.object_id = fk.referenced_object_id and rc.column_id = fkc.referenced_column_id
        where sc.object_id = object_id(@Table)
        order by sc.column_id;        
        
        """;

        var dm = await _dbContext.LoadModelSqlAsync(null, sqlString, prms =>
        {
            prms.AddString("@Table", table);
        });

        var columns = dm.Eval<List<ExpandoObject>>("Columns") ?? [];

        return columns;
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;

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

        cmd.SetAction(r => JsonResult.Try(() => ColumnList(r.GetValue(tableArg)!)));
        return cmd;
    }

    private Task ColumnList(String table)
    {
        var colsSql = """
        select sc.[name], [type] = tp.[name],  sc.max_length, ref_schema = rs.[name], ref_table = rt.[name], ref_column = rc.[name]
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

        var refsToMeSql = """
        select
            fk_schema = ss.[name], fk_table = sp.[name], fk_column = sc.[name], ref_schema = rs.[name],
            ref_table = rp.[name], ref_column = rc.[name], constraint_name = fk.[name]
        from sys.foreign_keys fk
        	join sys.foreign_key_columns fkc on fkc.constraint_object_id = fk.object_id
        	join sys.objects sp on sp.object_id = fk.parent_object_id
        	join sys.schemas ss on ss.schema_id = sp.schema_id
        	join sys.objects rp on rp.object_id = fk.referenced_object_id
        	join sys.schemas rs on rs.schema_id = rp.schema_id
        	join sys.columns sc on sc.object_id = fk.parent_object_id and sc.column_id  = fkc.parent_column_id
        	join sys.columns rc on rc.object_id = fk.referenced_object_id and rc.column_id  = fkc.referenced_column_id
        where  fk.referenced_object_id  = object_id(@Table)
        order by ss.[name], sp.[name], sc.[name];
        """;
        return Task.FromResult(0);
    }
}

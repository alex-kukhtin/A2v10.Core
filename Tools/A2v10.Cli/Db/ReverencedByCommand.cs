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

internal class ReferencedByCommand(IServiceProvider services)
{
    private readonly IDbContext _dbContext = services.GetRequiredService<IDbContext>();
    internal Command Build()
    {
        var cmd = new Command("referenced-by", "List tables and columns that reference the given table");
        var tableArg = new Argument<String>("table")
        {
            Description = "Table name",
        };
        cmd.Arguments.Add(tableArg);

        cmd.SetAction(r => JsonResult.Try(() => ReferencedByList(r.GetValue(tableArg)!)));
        return cmd;
    }

    private async Task<Object> ReferencedByList(String table)
    {
        var sqlString = """
        set nocount on;
        set transaction isolation level read uncommitted;

        select [ReferencedBy!TRefBy!Array] = null, 
            [Table] = ss.[name] + '.[' + sp.[name] + ']', [Column] = sc.[name]
        	--ref_schema = rs.[name], ref_table = rp.[name], ref_column = rc.[name], 
        	--[Constraint] = fk.[name]
        from sys.foreign_keys fk
            join sys.foreign_key_columns fkc on fkc.constraint_object_id = fk.object_id
            join sys.objects sp on sp.object_id = fk.parent_object_id
            join sys.schemas ss on ss.schema_id = sp.schema_id
            join sys.objects rp on rp.object_id = fk.referenced_object_id
            join sys.schemas rs on rs.schema_id = rp.schema_id
            join sys.columns sc on sc.object_id = fk.parent_object_id and sc.column_id  = fkc.parent_column_id
            join sys.columns rc on rc.object_id = fk.referenced_object_id and rc.column_id  = fkc.referenced_column_id
        where  fk.referenced_object_id  = object_id(@Table)
            and sc.[name] not in (N'Tenant', N'TenantId')
        order by ss.[name], sp.[name], sc.[name];
                
        """;

        var dm = await _dbContext.LoadModelSqlAsync(null, sqlString, prms =>
        {
            prms.AddString("@Table", table);
        });

        var columns = dm.Eval<List<ExpandoObject>>("ReferencedBy") ?? [];

        return columns;
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.CommandLine;
using System.Threading.Tasks;
using System.Dynamic;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using A2v10.Data.Interfaces;
using A2v10.Data.Core.Extensions.Dynamic;
using A2v10.Data.Core.Extensions;

namespace A2v10.Cli;

internal class TablesCommand(IServiceProvider services)
{
    private readonly IDbContext _dbContext = services.GetRequiredService<IDbContext>();
    internal Command Build()
    {
        var cmd = new Command("tables", "List database tables");
        var schemaArg = new Argument<String?>("schema")
        {
            Description = "Filter by schema name",
            DefaultValueFactory = reslut => null
        };
        cmd.Arguments.Add(schemaArg);

        cmd.SetAction(r => JsonResult.Try(() =>  DbList(r.GetValue(schemaArg))));
        return cmd;
    }
    async Task DbList(String? schema)
    {
        var sqlString = """
        set nocount on;
        set transaction isolation level read uncommitted;

        select [Tables!TTable!Array] = null, [Schema] = TABLE_SCHEMA, [Table] = TABLE_NAME 
        from INFORMATION_SCHEMA.TABLES
        where TABLE_SCHEMA not in ('a2wf', 'a2sys', 'a2meta', 'dbo', 'a2security') and
            @Schema is null or TABLE_SCHEMA = @Schema;        
        """;

        var dm = await _dbContext.LoadModelSqlAsync(null, sqlString, prms =>
        {
            prms.AddString("@Schema", schema);
        });

        var tables = dm.Eval<List<ExpandoObject>>("Tables")?.Select(x => $"{x.Get<String>("Schema")}.[{x.Get<String>("Table")}]");

        JsonResult.Ok(tables); 
    }
}

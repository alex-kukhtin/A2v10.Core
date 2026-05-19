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
        return Task.FromResult(0);
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Cli;

internal class InfoCommand(IServiceProvider services)
{
    private readonly IDbContext _dbContext = services.GetRequiredService<IDbContext>();
    private readonly DbTarget _target = services.GetRequiredService<DbTarget>();

    private const Int32 CANNOT_OPEN_DATABASE = 4060;

    internal Command Build()
    {
        var cmd = new Command("info", "Show the database the application is connected to");

        cmd.SetAction(r => JsonResult.Try(() => Info()));
        return cmd;
    }

    private async Task<Object> Info()
    {
        _target.EnsureNotSystem();

        var (exists, platform) = await Probe();

        return new ExpandoObject()
        {
            { "server", _target.Server },
            { "database", _target.Database },
            { "source", _target.Source },
            { "exists", exists },
            { "platform", platform }
        };
    }

    private async Task<(Boolean Exists, Boolean Platform)> Probe()
    {
        var sqlString = """
        set nocount on;
        set transaction isolation level read uncommitted;

        select [Info!TInfo!Object] = null,
            [Platform] = case when exists(
                select * from INFORMATION_SCHEMA.TABLES
                where TABLE_SCHEMA = N'a2security' and TABLE_NAME = N'Users')
            then cast(1 as bit) else cast(0 as bit) end;
        """;

        try
        {
            var dm = await _dbContext.LoadModelSqlAsync(null, sqlString, prms => { });
            return (true, dm.Eval<Boolean>("Info.Platform"));
        }
        catch (Exception ex) when (IsDatabaseNotFound(ex))
        {
            // the database is named in the connection string but does not exist on the server
            return (false, false);
        }
    }

    private static Boolean IsDatabaseNotFound(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            if (e is SqlException sqlEx && sqlEx.Number == CANNOT_OPEN_DATABASE)
                return true;
        return false;
    }
}

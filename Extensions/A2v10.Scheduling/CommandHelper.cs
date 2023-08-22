// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Scheduling;

public class ScheduledCommandHelper
{
    private readonly IDbContext _dbContext;
    public ScheduledCommandHelper(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public Task QueueCommandAsync(String? dataSource, ScheduledCommand command)
    {
        return _dbContext.ExecuteAsync<ScheduledCommand>(dataSource, "a2sch.[Command.Queue]", command);
    }

    public Task QueueCommandsAsync(String? dataSource, IEnumerable<ScheduledCommand> commands)
    {
        return _dbContext.SaveListAsync<ScheduledCommand>(dataSource, "a2sch.[Command.Collection.Queue]", null, commands);
    }
}


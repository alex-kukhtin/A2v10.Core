// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Scheduling.Infrastructure;

namespace A2v10.Scheduling;

public class ProcessCommandsJobHandler : IScheduledJob
{
    private readonly ILogger<ProcessCommandsJobHandler> _logger;
    private readonly IDbContext _dbContext;
    private readonly ScheduledCommandProvider _commandProvider;
    private readonly IServiceProvider _serviceProvider;
    public ProcessCommandsJobHandler(ILogger<ProcessCommandsJobHandler> logger, IServiceProvider serviceProvider, ScheduledCommandProvider commandProvider, IDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
        _commandProvider = commandProvider;
        _serviceProvider = serviceProvider; 
    }
    public async Task ExecuteAsync(ScheduledJobInfo jobInfo)
    {
        try
        {
            _logger.LogInformation("ProcessCommandsJob at {Time}, DataSource = {ds}", DateTime.Now, jobInfo.DataSource);
            var list = await _dbContext.LoadListAsync<CommandJobData>(jobInfo.DataSource, "a2sch.[Command.List]", null);
            if (list == null || list.Count == 0)
                return;
            foreach (var itm in list)
            {
                await ProcessCommand(itm, jobInfo.DataSource);
            }
        }
        catch (Exception ex)
        {
            await jobInfo.WriteException(_dbContext, ex);
            _logger.LogCritical("Failed to execute {ex}", ex);
        }
    }

    private async Task ProcessCommand(CommandJobData job, String? dataSource)
    {
        try
        {
            var commandType = _commandProvider.FindCommand(job.Command);
            if (_serviceProvider.GetRequiredService(commandType) is not IScheduledCommand commandHandler)
                throw new InvalidOperationException($"Type {commandType} does not implement IScheduledCommand");
            await commandHandler.ExecuteAsync(job.Data);
            await WriteComplete(dataSource, job, true);
        }
        catch (Exception ex)
        {
			_logger.LogCritical("Failed to command '{cmd}'. {ex}", job.Command, ex);
			await WriteComplete(dataSource, job, false, ex);
        }
    }

    private Task WriteComplete(String? dataSource, CommandJobData job, Boolean success, Exception? ex = null)
    {
        var prms = new ExpandoObject();
        prms.TryAdd("Id", job.Id);
        prms.TryAdd("Complete", success);
        prms.TryAdd("Lock", job.Lock);

        if (ex != null)
        {
            if (ex.InnerException != null)
                ex = ex.InnerException;
            prms.TryAdd("Error", ex.Message);
        }
        return _dbContext.ExecuteExpandoAsync(dataSource, "a2sch.[Command.Complete]", prms);
    }
}

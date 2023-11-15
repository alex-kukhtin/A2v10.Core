// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Quartz;

using A2v10.Scheduling.Infrastructure;

namespace A2v10.Scheduling;

internal class GenericJob(ILogger<GenericJob> logger, IServiceProvider serviceProvider) : IJob
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger _logger = logger;

    public async Task Execute(IJobExecutionContext context)
    {
        var handler = context.MergedJobDataMap.Get("HandlerType") as Type;
        var info = context.MergedJobDataMap.Get("JobInfo") as ScheduledJobInfo;
        if (info == null)
        {
            _logger.LogCritical("'JobInfo' not found");
            return;
        }
        if (handler == null)
        {
            _logger.LogCritical("Handler is null");
            return;
        }
        try
        {
            if (_serviceProvider.GetRequiredService(handler) is IScheduledJob schedulingJob)
                await schedulingJob.ExecuteAsync(info);
            else
                _logger.LogCritical("{handler} is not registered", handler);
        }
        catch (Exception ex)
        {
            _logger.LogCritical("Failed to execute {ex}", ex);
        }
    }
}

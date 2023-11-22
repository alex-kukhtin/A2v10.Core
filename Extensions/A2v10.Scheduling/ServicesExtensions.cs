// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Quartz;

using A2v10.Scheduling.Infrastructure;

namespace A2v10.Scheduling;

public static class ServicesExtensions
{
	public static IServiceCollection UseScheduling(this IServiceCollection services, IConfiguration configuration, Action<AddSchedulerHandlerFactory> action)
	{
		var section = configuration.GetSection("scheduler");
		if (section == null)
			return services;
		var sc = new SchedulerConfig();
		section.Bind(sc);
		if (sc.Jobs == null || sc.Jobs.Length == 0)
			return services;

		var factory = new AddSchedulerHandlerFactory();
		action.Invoke(factory);

		RegisterCommands(factory, services);

        foreach (var job in sc.Jobs)
			ValidateJob(job);

		services.AddQuartz(q =>
		{
			foreach (var job in sc.Jobs)
			{
                var handlerType = factory.FindHandler(job.Handler);
                if (!handlerType.IsAssignableTo(typeof(IScheduledJob)))
                    throw new InvalidOperationException($"Type {handlerType} does not implement ISchedulingJob");
				services.TryAddScoped(handlerType);
                AddJob(q, job, handlerType);
			}
		});

		services.AddQuartzHostedService(opts =>
		{
			opts.WaitForJobsToComplete = true;
		});

		return services;
	}

    private static void RegisterCommands(AddSchedulerHandlerFactory factory, IServiceCollection services)
	{
		// always
        services.AddSingleton<ScheduledCommandProvider>(new ScheduledCommandProvider(factory.Commands));

        if (factory.Commands.Count == 0)
			return;
		foreach (var (_, type) in factory.Commands)
		{
            if (!type.IsAssignableTo(typeof(IScheduledCommand)))
                throw new InvalidOperationException($"Type {type} does not implement IScheduledCommand");
            services.AddScoped(type);
		}
    }


    private static void AddJob(IServiceCollectionQuartzConfigurator qc, ConfigJob job, Type handlerType)
	{
		if (job == null)
			return;

		var jobKey = new JobKey(job.Id);

		var map = new JobDataMap()
		{
			{ "HandlerType", handlerType },
			{ "JobInfo",  job.JobInfo }
		};

		qc.AddJob(typeof(GenericJob), jobKey, opts => opts.SetJobData(map));

		qc.AddTrigger(opts =>
			opts.ForJob(jobKey)
			.WithIdentity($"{job.Id}_Trigger")
			.WithCronSchedule(job.Cron)
		);
	}

	private static void ValidateJob(ConfigJob job)
	{
		if (String.IsNullOrEmpty(job.Id))
			throw new ConfigurationErrorsException("Job Id is required");
		if (!CronExpression.IsValidExpression(job.Cron))
			throw new ConfigurationErrorsException($"Invalid cron expression '{job.Cron}'");
	}
}

// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Configuration;
using A2v10.Scheduling.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Quartz;

namespace A2v10.Scheduling;

public static class ServicesExtensions
{
	public static IServiceCollection UseScheduling(this IServiceCollection services, IConfiguration configuration)
	{
		var section = configuration.GetSection("scheduler");
		if (section == null)
			return services;
		var sc = new SchedulerConfig();
		section.Bind(sc);
		if (sc.Jobs == null || sc.Jobs.Length == 0)
			return services;

		foreach (var job in sc.Jobs)
			ValidateJob(job);

		services.AddQuartz(q =>
		{
			q.UseMicrosoftDependencyInjectionJobFactory();
			foreach (var job in sc.Jobs)
				AddJob(q, job);
		});

		services.AddQuartzHostedService(opts =>
		{
			opts.WaitForJobsToComplete = true;
		});

		return services;
	}

	private static void AddJob(IServiceCollectionQuartzConfigurator qc, ConfigJob job)
	{
		if (job == null)
			return;

		var jobKey = new JobKey(job.Id);

		var jmap = new JobDataMap() {
			{ "JobInfo", job.SqlJobInfo }
		};

		var jobType = JobTypeFactory.GetJobType(job.Command);

		qc.AddJob(jobType, jobKey, opts => opts.SetJobData(jmap));

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

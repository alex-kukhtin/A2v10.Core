// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Quartz;

using A2v10.Data.Interfaces;

namespace A2v10.Scheduling.Jobs;

internal class ExecuteSqlJob : IJob
{
	private readonly ILogger<ExecuteSqlJob> _logger;
	private readonly IDbContext _dbContext;
	public ExecuteSqlJob(ILogger<ExecuteSqlJob> logger, IDbContext dbContext)
	{
		_logger = logger;
		_dbContext = dbContext;
	}
	public async Task Execute(IJobExecutionContext context)
	{
		var info = context.MergedJobDataMap.Get("JobInfo") as SqlJobInfo;
		if (info == null)
		{
			_logger.LogCritical("JobInfo not found");
			return;
		}
		try
		{
			var proc = info.Procedure ??
				throw new InvalidOperationException("Procedure is null");
			_logger.LogInformation("ExecuteSqlJob at {Time}, DataSource = {ds}, Procedure = {proc}", DateTime.Now, info.DataSource, info.Procedure);
			await _dbContext.ExecuteExpandoAsync(info.DataSource, info.Procedure, info.Parameters ?? new System.Dynamic.ExpandoObject());
		} 
		catch (Exception ex)
		{
			_logger.LogCritical("Failed to execute {ex}", ex);
		}
	}
}

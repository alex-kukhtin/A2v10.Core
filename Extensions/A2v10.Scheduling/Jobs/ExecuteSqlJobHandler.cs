// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using A2v10.Data.Interfaces;
using A2v10.Scheduling.Infrastructure;

namespace A2v10.Scheduling;

public class ExecuteSqlJobHandler : IScheduledJob
{
	private readonly ILogger<ExecuteSqlJobHandler> _logger;
	private readonly IDbContext _dbContext;
	public ExecuteSqlJobHandler(ILogger<ExecuteSqlJobHandler> logger, IDbContext dbContext)
	{
		_logger = logger;
		_dbContext = dbContext;
	}
    public async Task ExecuteAsync(ScheduledJobInfo info)
    {
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

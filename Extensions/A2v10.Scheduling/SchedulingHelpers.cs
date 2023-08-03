// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Scheduling.Infrastructure;

namespace A2v10.Scheduling;

public static class SchedulingHelpers
{
	public static Task WriteException(this ScheduledJobInfo jobInfo, IDbContext dbContext, Exception ex)
	{
		if (ex.InnerException != null)
			ex = ex.InnerException;
		String message = ex.Message;
		if (message.Length > 255)
			message = message[..255];
		var prms = new ExpandoObject();
		prms.TryAdd("JobId", jobInfo.Id ?? "command");
		prms.TryAdd("Message", message);
		return dbContext.ExecuteExpandoAsync(jobInfo.DataSource, "a2sch.[Exception]", prms);
	}
}

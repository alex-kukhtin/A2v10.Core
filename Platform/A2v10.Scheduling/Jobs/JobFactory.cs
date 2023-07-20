// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Scheduling.Jobs;

internal static class JobTypeFactory
{
	public static Type GetJobType(JobCommand command)
	{
		return command switch
		{
			JobCommand.ExecuteSql => typeof(ExecuteSqlJob),
			JobCommand.InvokeClr => typeof(InvokeClrJob),
			_ => throw new NotImplementedException()
		};
	}
}

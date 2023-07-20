// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Quartz;

namespace A2v10.Scheduling;

internal class InvokeClrJob : IJob
{
	private readonly IServiceProvider _serviceProvider;
	public InvokeClrJob(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;	
	}
	public Task Execute(IJobExecutionContext context)
	{
		return Task.CompletedTask;
	}
}

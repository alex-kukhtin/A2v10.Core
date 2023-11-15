// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using A2v10.Scheduling.Infrastructure;
using System;
using System.Dynamic;

namespace A2v10.Scheduling;

internal record ConfigJob
{
	public String Id { get; set; } = String.Empty;
	public String Cron { get; set; }= String.Empty;
	public String? DataSource { get; set; }
	public ExpandoObject? Parameters { get; set; }
	public String Handler { get; set; } = String.Empty;
	public String? Procedure { get; set; }
	internal ScheduledJobInfo JobInfo => new()
	{
		Id = Id,
		DataSource = this.DataSource,
		Procedure = this.Procedure,
		Parameters = this.Parameters
	};
}

internal class SchedulerConfig
{
	public ConfigJob[] Jobs { get; set; } = [];
}
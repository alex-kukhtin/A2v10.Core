// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

namespace A2v10.Scheduling;

public record SqlJobInfo
{
	public String? DataSource { get; init; }
	public String? Procedure { get; init; }
	public ExpandoObject? Parameters { get; init; }
}

internal enum JobCommand
{
	ExecuteSql,
	InvokeClr
}
internal record ConfigJob
{
	public String Id { get; set; } = String.Empty;
	public String Cron { get; set; }= String.Empty;
	public String? ClrType { get; set; }
	public String? DataSource { get; set; }
	public ExpandoObject? Parameters { get; set; }
	public JobCommand Command { get; set; }
	public String? Procedure { get; set; }
	/*
				"Invoke": "Commands",
				"Parameters": {
					"Number": "5",
					"Boolean": "true",
					"String": "string 1"
	*/
	internal SqlJobInfo SqlJobInfo => new()
	{
		DataSource = this.DataSource,
		Procedure = this.Procedure,
		Parameters = this.Parameters
	};
}

internal class SchedulerConfig
{
	public ConfigJob[] Jobs { get; set; } = Array.Empty<ConfigJob>();
}
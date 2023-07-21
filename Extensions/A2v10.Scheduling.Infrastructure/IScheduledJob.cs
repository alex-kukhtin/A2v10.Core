// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Scheduling.Infrastructure;

public record ScheduledJobInfo
{
    public String? Id { get; set; }
    public String? DataSource { get; init; }
    public String? Procedure { get; init; }
    public ExpandoObject? Parameters { get; init; }
}

public interface IScheduledJob
{
    Task ExecuteAsync(ScheduledJobInfo info);
}
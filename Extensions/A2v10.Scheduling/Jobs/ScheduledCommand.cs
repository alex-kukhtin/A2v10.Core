// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Scheduling;

public record ScheduledCommand
{
    // for dynamic creation
    public ScheduledCommand()
    {
        Command = String.Empty; 
    }
    public ScheduledCommand(String command, String? data = null, DateTime? utcRunAt = null)
    {
        Command = command;
        Data = data;
        UtcRunAt = utcRunAt;
    }
    public String Command { get; init; } 
    public String? Data { get; init; }
    public DateTime? UtcRunAt { get; init; }
}

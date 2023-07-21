// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Scheduling;

internal record CommandJobData
{
    public Int64 Id { get; set; }
    public String Command { get; set; } = String.Empty; 
    public String? Data { get; set; }
    public Guid Lock { get; set; }
}

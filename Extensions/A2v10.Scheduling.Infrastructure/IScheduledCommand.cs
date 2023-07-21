// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Scheduling.Infrastructure;

public interface IScheduledCommand
{
    Task ExecuteAsync(String? Data);
}
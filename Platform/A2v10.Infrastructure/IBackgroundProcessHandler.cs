// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface IBackgroundProcessHandler
{
    void Execute(Func<IServiceProvider, Task> action);
}

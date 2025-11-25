// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.App.Infrastructure;

public record CancelToken
{
    public Boolean Cancel { get; set; }
    public String? Message { get; set; }
}

public interface IClrElementEventSource
{
    Func<CancelToken, Task>? BeforeSave { get; }
    Func<Task>? AfterSave { get; }
}

public interface IClrDocumentEventSource : IClrElementEventSource
{
}

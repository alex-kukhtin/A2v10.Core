// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

namespace A2v10.Infrastructure;

public record SignalResult : ISignalResult
{
    public SignalResult(Int64 userId, String message, ExpandoObject? data = null)
    {
        UserId = userId; 
        Message = message; 
        Data = data;
    }
    public Int64 UserId { get; init; }
    public String Message { get; init; }
    public ExpandoObject? Data { get; init; }
}

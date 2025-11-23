// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Text;

namespace A2v10.Infrastructure;

public record EmptyInvokeResult : IInvokeResult
{
    public Byte[] Body { get; init; } = [];

    public String ContentType { get; init; } = String.Empty;

    public String? FileName { get; init; }

    public ISignalResult? Signal { get; init; }

    public static IInvokeResult FromString(String str, String contentType)
    {
        return new EmptyInvokeResult()
        {
            Body = Encoding.UTF8.GetBytes(str),
            ContentType = contentType
        };
    }
}

// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure;

public class RenderResult(String body, String contentType) : IRenderResult
{
    public String Body { get; } = body;
    public String ContentType { get; } = contentType;
}

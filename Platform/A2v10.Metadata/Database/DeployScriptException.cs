// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

public sealed class DeployScriptException(Exception inner, Int32 lineFrom, Int32 lineTo) 
    : Exception(inner.Message, inner)
{
    public Int32 LineFrom => lineFrom;
    public Int32 LineTo => lineTo;
}

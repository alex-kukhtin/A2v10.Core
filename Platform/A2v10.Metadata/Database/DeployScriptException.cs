// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

// the batch that failed, as coordinates in the file it was executed from - the text is on disk
public sealed class DeployScriptException(Exception inner, String file, Int32 lineFrom, Int32 lineTo)
    : Exception(inner.Message, inner)
{
    public String File => file;
    public Int32 LineFrom => lineFrom;
    public Int32 LineTo => lineTo;
}

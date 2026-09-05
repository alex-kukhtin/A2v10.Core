// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

public class NullReportEngineProvider : IReportEngineProvider
{
    public IReportEngine FindReportEngine(string name)
    {
        throw new NotImplementedException("Install Real Report Engine");
    }
}

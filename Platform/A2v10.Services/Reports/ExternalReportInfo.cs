// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

using A2v10.Data.Interfaces;

namespace A2v10.Services;
public record ExternalReportInfo : IReportInfo
{
    public ExternalReportInfo(String report, String path)
    {
        Report = report;
        Path = path;
    }
    public Stream? Stream { get; init; }
    public String? Name { get; init; }
    public String Path { get; }
    public String Report { get; }
    public IDataModel? DataModel { get; init; }
    public ExpandoObject? Variables { get; init; }
}


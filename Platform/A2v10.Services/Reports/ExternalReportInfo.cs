// Copyright © 2021 Alex Kukhtin. All rights reserved.

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


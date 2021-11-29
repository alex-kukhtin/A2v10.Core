// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;
public interface IReportInfo
{
	Stream? Stream { get; }
	String? Name { get; }
	String Path { get; }
	String Report { get; }
	IDataModel? DataModel { get; }
	ExpandoObject? Variables { get; }
}

public interface IReportEngine
{
	Task<IInvokeResult> ExportAsync(IReportInfo reportInfo, ExportReportFormat format);
}


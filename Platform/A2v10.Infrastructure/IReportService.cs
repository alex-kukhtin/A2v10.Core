// Copyright © 2021-2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public enum ExportReportFormat
{
	Undefined,
	Pdf,
	Excel,
	Word,
	OpenSheet,
	OpenText
};

public interface IReportService
{
	Task<IInvokeResult> ExportAsync(String url, ExportReportFormat format, Action<ExpandoObject> setParams);
	Task<IReportInfo> GetReportInfoAsync(String url, Action<ExpandoObject> setParams);
}

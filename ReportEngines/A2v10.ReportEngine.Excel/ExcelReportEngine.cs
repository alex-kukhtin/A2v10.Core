// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.ReportEngine.Script;
using A2v10.Xaml.Report.Spreadsheet;

namespace A2v10.ReportEngine.Excel;

public class ExcelReportEngine(ILocalizer _localizer, ICurrentUser _user) : IReportEngine
{
	private readonly IReportLocalizer _localizer = new DefaultReportLocalizer(_user.Locale.Locale, _localizer);

	public Task<IInvokeResult> ExportAsync(IReportInfo reportInfo, ExportReportFormat format)
	{
		String repPath = String.Empty;
		if (!reportInfo.Report.StartsWith("{{") || !reportInfo.Report.EndsWith("}}"))
			throw new InvalidOperationException("ExcelReportEngine.ReadFromTemplate. Yet not implemented");

		Spreadsheet sheet = ReadTemplateFromDb(reportInfo);

		var name = reportInfo.DataModel?.Root?.Resolve(reportInfo.Name) ?? "report";

		var model = reportInfo.DataModel?.Root ?? [];
		var context = new RenderContext(repPath, _localizer, model, sheet.Code);

		throw new InvalidOperationException("Yet not implemented");
	}

	private static Spreadsheet ReadTemplateFromDb(IReportInfo reportInfo)
	{
		var json = reportInfo.DataModel?.Resolve(reportInfo.Report)
			?? throw new InvalidOperationException("Data is null");
		var ss = SpreadsheetJson.FromJson(json);
		return ss;
	}
}

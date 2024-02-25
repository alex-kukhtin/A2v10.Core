// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;

using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Infrastructure;
using A2v10.Xaml.Report;

using A2v10.Xaml.Report.Spreadsheet;
using A2v10.ReportEngine.Script;
using DocumentFormat.OpenXml.Bibliography;

namespace A2v10.ReportEngine.Pdf;

public class PdfReportEngine : IReportEngine
{
	private readonly IAppCodeProvider _appCodeProvider;
	private readonly IReportLocalizer _localizer;

	public PdfReportEngine(IAppCodeProvider appCodeProvider, ILocalizer localizer, ICurrentUser user)
	{
        Settings.License ??= LicenseType.Community;
        _appCodeProvider = appCodeProvider;
		_localizer = new DefaultReportLocalizer(user.Locale.Locale, localizer);
	}

	private Page ReadTemplate(String path)
	{
		using var stream = _appCodeProvider.FileStreamRO(path)
			?? throw new InvalidOperationException($"File not found '{path}'");
		return TemplateReader.ReadReport(stream);
	}

	private static Spreadsheet ReadTemplateFromDb(IReportInfo reportInfo)
	{
		var json = reportInfo.DataModel?.Resolve(reportInfo.Report)
			?? throw new InvalidOperationException("Data is null");
		var ss = SpreadsheetJson.FromJson(json);
		ss.ApplyStyles("Root", new StyleBag());
		return ss;
	}

	public Task<IInvokeResult> ExportAsync(IReportInfo reportInfo, ExportReportFormat format)
	{
		String repPath = String.Empty;
		Boolean readFromModel = false;

		if (reportInfo.Report.StartsWith("{{") && reportInfo.Report.EndsWith("}}"))
			readFromModel = true;
		else 
			repPath = Path.Combine(reportInfo.Path, reportInfo.Report) + ".xaml";

		Page page = readFromModel ? ReadTemplateFromDb(reportInfo) : ReadTemplate(repPath);

		if (page.Title == null && reportInfo.Name != null)
			page.Title = reportInfo.Name;

		var name = reportInfo.DataModel?.Root?.Resolve(reportInfo.Name) ?? "report";

		var model = reportInfo.DataModel?.Root ?? [];
		var context = new RenderContext(repPath, _localizer, model, page.Code);
		var doc = new ReportDocument(page, context);

		var resultTitle = doc.GetMetadata().Title;
		if (!String.IsNullOrEmpty(resultTitle))
			name = resultTitle;

		using MemoryStream outputStream = new();

		doc.GeneratePdf(outputStream);
		var result = new PdfInvokeResult(outputStream.ToArray(), name + ".pdf");
		return Task.FromResult<IInvokeResult>(result);
	}
}
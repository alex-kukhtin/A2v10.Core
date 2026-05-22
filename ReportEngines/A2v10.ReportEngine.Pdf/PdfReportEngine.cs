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

namespace A2v10.ReportEngine.Pdf;

public class PdfReportEngine : IReportEngine
{
	private readonly IAppCodeProvider _appCodeProvider;
	private readonly IReportLocalizer _localizer;

	public PdfReportEngine(IAppCodeProvider appCodeProvider, ILocalizer localizer, ICurrentUser user)
	{
        Settings.License ??= LicenseType.Community;

		// Settings.EnableDebugging = true;

		_appCodeProvider = appCodeProvider;
		_localizer = new DefaultReportLocalizer(user.Locale.Locale, localizer);
	}

	private (Page page, String path)  ReadTemplate(String pathA, String pathX)
	{
		using var streamA = _appCodeProvider.FileStreamRO(pathA);
		if (streamA != null)
            return new (TemplateReader.ReadReport(streamA), pathA);
        using var streamX = _appCodeProvider.FileStreamRO(pathX);
        if (streamX != null)
            return new (TemplateReader.ReadReport(streamX), pathX);
		throw new InvalidOperationException($"File not found '{pathA}' or '{pathX}'");
	}

	private static (Page page, String path)  ReadTemplateFromDb(IReportInfo reportInfo, String path)
	{
		var json = reportInfo.DataModel?.Resolve(reportInfo.Report)
			?? throw new InvalidOperationException("Data is null");
		var ss = SpreadsheetJson.FromJson(json);
		ss.ApplyStyles("Root", new StyleBag());
		return new (ss, path);
	}

	public Task<IInvokeResult> ExportAsync(IReportInfo reportInfo, ExportReportFormat format)
	{
		String repPathA = String.Empty;
        String repPathX = String.Empty;
        Boolean readFromModel = false;

		if (reportInfo.Report.StartsWith("{{") && reportInfo.Report.EndsWith("}}"))
			readFromModel = true;
		else
		{
			repPathA = Path.Combine(reportInfo.Path, reportInfo.Report) + ".xamla";
            repPathX = Path.Combine(reportInfo.Path, reportInfo.Report) + ".xaml";
        }

        var (page, path)  = readFromModel ? ReadTemplateFromDb(reportInfo, repPathA) : ReadTemplate(repPathA, repPathX);

		if (page.Title == null && reportInfo.Name != null)
			page.Title = reportInfo.Name;

		var name = reportInfo.DataModel?.Root?.Resolve(reportInfo.Name) ?? "report";

		var model = reportInfo.DataModel?.Root ?? [];
		var context = new RenderContext(path, _localizer, model, page.Code);
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
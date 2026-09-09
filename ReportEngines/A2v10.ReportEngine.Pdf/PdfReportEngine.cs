// Copyright © 2022-2026 Oleksandr Kukhtin. All rights reserved.

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

	private Page ReadTemplate(String pathA, String pathX)
	{
		using var streamA = _appCodeProvider.FileStreamRO(pathA);
		if (streamA != null)
            return TemplateReader.ReadReport(streamA);
        using var streamX = _appCodeProvider.FileStreamRO(pathX);
        if (streamX != null)
            return TemplateReader.ReadReport(streamX);
		throw new InvalidOperationException($"File not found '{pathA}' or '{pathX}'");
	}

	private static Page ReadTemplateFromDb(IReportInfo reportInfo)
	{
		var json = reportInfo.DataModel?.Resolve(reportInfo.Report)
			?? throw new InvalidOperationException("Data is null");
		var ss = SpreadsheetJson.FromJson(json);
		ss.ApplyStyles("Root", new StyleBag());
		return ss;
	}

    private static Page ReadTemplateFromStream(Stream stream)
    {
		using var sr = new StreamReader(stream);
        var json = sr.ReadToEnd();
        var ss = SpreadsheetJson.FromJson(json);
        ss.ApplyStyles("Root", new StyleBag());
        return ss;
    }

    public Task<IInvokeResult> ExportAsync(IReportInfo reportInfo, ExportReportFormat format)
	{
		String repPathA = String.Empty;
        String repPathX = String.Empty;
        Boolean readFromModel = false;
		Boolean readFromStream = false;

		if (reportInfo.Stream != null)
			readFromStream = true;
		else if (reportInfo.Report.StartsWith("{{") && reportInfo.Report.EndsWith("}}"))
			readFromModel = true;
		else
		{
			repPathA = Path.Combine(reportInfo.Path, reportInfo.Report) + ".vxaml";
			repPathX = Path.Combine(reportInfo.Path, reportInfo.Report) + ".xaml";
		}

        var page =
			readFromStream ? ReadTemplateFromStream(reportInfo.Stream!)
			: readFromModel ? ReadTemplateFromDb(reportInfo)
			: ReadTemplate(repPathA, repPathX);

		if (page.Title == null && reportInfo.Name != null)
			page.Title = reportInfo.Name;

		var name = reportInfo.DataModel?.Root?.Resolve(reportInfo.Name) ?? "report";

		var model = reportInfo.DataModel?.Root ?? [];
		// Папка отчёта на всех трёх маршрутах, а не только на файловом: файлы, названные
		// изнутри бланка, лежат рядом с ним и тогда, когда сам бланк приехал из базы
		var context = new RenderContext(_appCodeProvider, reportInfo.Path, _localizer, model, page.Code);
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
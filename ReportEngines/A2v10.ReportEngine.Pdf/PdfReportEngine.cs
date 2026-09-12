// Copyright © 2022-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Infrastructure;
using A2v10.Xaml.Report;

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

	// Маршруты отличаются только тем, откуда берутся байты; что в них — XAML или JSON —
	// решает один распознаватель в TemplateReader, одинаково для всех трёх
	private Page ReadTemplate(IReportInfo reportInfo)
	{
		if (reportInfo.Stream != null)
			return TemplateReader.ReadReport(reportInfo.Stream);
		if (reportInfo.Report.StartsWith("{{") && reportInfo.Report.EndsWith("}}"))
		{
			var text = reportInfo.DataModel?.Resolve(reportInfo.Report)
				?? throw new InvalidOperationException("Data is null");
			return TemplateReader.ReadReport(Encoding.UTF8.GetBytes(text));
		}
		using var file = ReportTemplateFile.Open(_appCodeProvider, Path.Combine(reportInfo.Path, reportInfo.Report));
		return TemplateReader.ReadReport(file);
	}

    public Task<IInvokeResult> ExportAsync(IReportInfo reportInfo, ExportReportFormat format)
	{
		var page = ReadTemplate(reportInfo);

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
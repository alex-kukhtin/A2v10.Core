// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;

using QuestPDF.Fluent;

using A2v10.Infrastructure;
using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

public class PdfReportEngine : IReportEngine
{
	private readonly IAppCodeProvider _appCodeProvider;
	private readonly IReportLocalizer _localizer;
	private readonly String _rootPath;

	public PdfReportEngine(IAppCodeProvider appCodeProvider, ILocalizer localizer, ICurrentUser user)
	{
		_appCodeProvider = appCodeProvider;
		_localizer = new PdfReportLocalizer(user.Locale.Locale, localizer);
		_rootPath = _appCodeProvider.MakeFullPath(String.Empty, String.Empty, false);
	}

	private Page ReadTemplate(IReportInfo reportInfo, String reportPath)
	{
		using var stream = _appCodeProvider.FileStreamFullPathRO(reportPath);
		return TemplateReader.ReadReport(stream);
	}
	public Task<IInvokeResult> ExportAsync(IReportInfo reportInfo, ExportReportFormat format)
	{
		String reportPath = _appCodeProvider.MakeFullPath(reportInfo.Path, $"{reportInfo.Report}.xaml", false);
		Page page = ReadTemplate(reportInfo, reportPath);

		var name = reportInfo.DataModel?.Root?.Resolve(reportInfo.Name) ?? "report";

		var model = reportInfo.DataModel?.Root ?? new ExpandoObject();
		var context = new RenderContext(_rootPath, reportPath, _localizer, model, page.Code);
		var doc = new ReportDocument(page, context);

		using MemoryStream outputStream = new();

		doc.GeneratePdf(outputStream);
		var result = new PdfInvokeResult(outputStream.ToArray(), name + ".pdf");
		return Task.FromResult<IInvokeResult>(result);
	}
}
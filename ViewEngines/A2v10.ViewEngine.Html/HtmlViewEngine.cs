// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.ViewEngine.Html;

public class HtmlViewEngine(IAppCodeProvider appCodeProvider, IProfiler profiler, /*IViewEngineProvider _engineProvider*/ ILocalizer localizer) : IViewEngine
{
	private readonly IProfiler _profiler = profiler;
	private readonly ILocalizer _localizer = localizer;
	private readonly IAppCodeProvider _appCodeProvider = appCodeProvider;

    public async Task<IRenderResult> RenderAsync(IRenderInfo renderInfo)
	{
        IProfileRequest request = _profiler.CurrentRequest;

		using var start = request.Start(ProfileAction.Render, $"load: {renderInfo.FileTitle}");

		if (renderInfo.FileName == null)
            throw new InvalidOperationException("HtmlViewEngine. FileName is null");
        var filePath = _appCodeProvider.MakePath(renderInfo.Path, renderInfo.FileName);
        var stream = _appCodeProvider.FileStreamRO(filePath)
            ?? throw new InvalidOperationException("HtmlViewEngine. Stream is null");

        using var tr = new StreamReader(stream);
        String htmlText = tr.ReadToEnd();
		if (!htmlText.Contains("$(RootId)"))
            throw new InvalidOperationException("HtmlViewEngine. $(RootId) macro not found");
        htmlText = htmlText.Replace("$(RootId)", renderInfo.RootId);
		htmlText = _localizer.Localize(null, htmlText, false)
			?? throw new InvalidOperationException("HtmlViewEngine. Html is null");

        using var sw = new StringWriter();
		await sw.WriteAsync(htmlText);
		return new RenderResult(
			body: sw.ToString(),
			contentType: MimeTypes.Text.HtmlUtf8
		);
	}
}
// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.


using System.IO;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.ViewEngine.Xaml;

public class XamlViewEngine(IProfiler profiler, IXamlPartProvider xamlPartProvider, ILocalizer localizer,
    IAppCodeProvider codeProvider) : IViewEngine
{
	private readonly XamlRenderer _renderer = new(profiler,
            xamlPartProvider,
            localizer,
            codeProvider
        );

    public Task<IRenderResult> RenderAsync(IRenderInfo renderInfo)
	{
		using var sw = new StringWriter();
		_renderer.Render(renderInfo, sw);
		var rr = new RenderResult(
			body: sw.ToString(),
			contentType: MimeTypes.Text.HtmlUtf8
		);
		return Task.FromResult<IRenderResult>(rr);
	}
}

// Copyright © 2022 Alex Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.ViewEngine.Html;

public class HtmlViewEngine : IViewEngine
{
	public HtmlViewEngine(IViewEngineProvider engineProvider)
	{
		engineProvider.RegisterEngine(".html", typeof(HtmlViewEngine));
	}

	public Task<IRenderResult> RenderAsync(IRenderInfo renderInfo)
	{
		using var sw = new StringWriter();
		var rr = new RenderResult(
			body: sw.ToString(),
			contentType: MimeTypes.Text.HtmlUtf8
		);
		return Task.FromResult<IRenderResult>(rr);
	}
}
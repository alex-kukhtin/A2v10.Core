// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Xaml
{
	public class XamlViewEngine : IViewEngine
	{
		private readonly IRenderer _renderer;

		public XamlViewEngine(IProfiler profiler, IAppCodeProvider codeProvider, ILocalizer localizer, IViewEngineProvider engineProvider)
		{
			engineProvider.RegisterEngine(".xaml", typeof(XamlViewEngine));

			_renderer = new XamlRenderer(profiler,
				codeProvider,
				new AppXamlReaderService(codeProvider),
				localizer
			);
		}

		public Task<IRenderResult> RenderAsync(IRenderInfo renderInfo)
		{
			using var sw = new StringWriter();
			_renderer.Render(renderInfo, sw);
			var rr = new RenderResult()
			{
				Body = sw.ToString(),
				ContentType = MimeTypes.Text.HtmlUtf8
			};
			return Task.FromResult<IRenderResult>(rr);
		}
	}
}

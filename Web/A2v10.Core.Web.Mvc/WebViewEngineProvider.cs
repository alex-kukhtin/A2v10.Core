// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;


namespace A2v10.Core.Web.Mvc
{
	public record ViewEngineDescriptor(String Extension, Type EngineType);

	public class WebViewEngineProvider : IViewEngineProvider
	{
		private readonly ViewEngineDescriptor[] _engines;
		private readonly IAppCodeProvider _codeProvider;
		private readonly IServiceProvider _serviceProvider;

		public WebViewEngineProvider(IServiceProvider serviceProvider, ViewEngineDescriptor[] views)
		{
			_serviceProvider = serviceProvider;
			_codeProvider = _serviceProvider.GetService<IAppCodeProvider>();
			_engines = views;
		}

		public IViewEngineResult FindViewEngine(String viewName)
		{
			foreach (var engine in _engines)
			{
				String fileName = $"{viewName}{engine.Extension}";
				if (_codeProvider.FileExists(fileName))
				{
					return new ViewEngineResult()
					{
						Engine = _serviceProvider.GetService(engine.EngineType) as IViewEngine,
						FileName = fileName
					};
				}
			}
			throw new InvalidReqestExecption($"View engine not found for {viewName}");
		}
	}
}

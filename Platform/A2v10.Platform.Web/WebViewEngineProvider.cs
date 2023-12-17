// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Platform.Web;
public record ViewEngineDescriptor(String Extension, Type EngineType);

public class ViewEngineFactory
{
	private readonly List<ViewEngineDescriptor> _list = [];

	public IList<ViewEngineDescriptor> Engines => _list;

	public void RegisterEngine<T>(String extension)
	{
		_list.Add(new ViewEngineDescriptor(extension, typeof(T)));
	}
}

public class WebViewEngineProvider : IViewEngineProvider
{
	private readonly IList<ViewEngineDescriptor> _engines;
	private readonly IAppCodeProvider _codeProvider;
	private readonly IServiceProvider _serviceProvider;

	public WebViewEngineProvider(IServiceProvider serviceProvider, IList<ViewEngineDescriptor> engines)
	{
		_serviceProvider = serviceProvider;
		_codeProvider = _serviceProvider.GetRequiredService<IAppCodeProvider>();
		_engines = engines;
	}

	public void RegisterEngine(String extension, Type engineType)
	{
		_engines.Add(new ViewEngineDescriptor(extension, engineType));
	}

	public IViewEngineResult FindViewEngine(String path, String viewName)
	{
		if (viewName == "@Model.View") {
			var xamlEngineDescriptor = _engines.First(x => x.Extension == ".xaml");
			var viewEngine = _serviceProvider.GetService(xamlEngineDescriptor.EngineType) as IViewEngine
				?? throw new InvalidOperationException("Invalid xaml engine type");
			return new ViewEngineResult( 
				engine: viewEngine,
				path: path,
				fileName: viewName
			);
		}
		foreach (var engine in _engines)
		{
			String fileName = $"{viewName}{engine.Extension}";
			String fullPath = _codeProvider.MakePath(path, fileName);
			if (_codeProvider.IsFileExists(fullPath))
			{
				if (_serviceProvider.GetService(engine.EngineType) is IViewEngine viewEngine)
				{
					return new ViewEngineResult
					(
						engine: viewEngine,
						path: fullPath,
						fileName: fileName
					);
				}
			}
		}
		var locations = String.Join('\n', _engines.Select(e => $"/{path}/{viewName}{e.Extension}"));

        throw new InvalidReqestExecption($"The view '{viewName}' was not found. The following locations were searched:\n{locations} ");
	}
}


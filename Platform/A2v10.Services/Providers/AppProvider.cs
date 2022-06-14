// Copyright © 2022 Alex Kukhtin. All rights reserved.

using Microsoft.Extensions.Options;

namespace A2v10.Services;

public class AppProvider : IAppProvider
{
	private readonly IAppContainer _appContainer;
	public AppProvider(IOptions<AppOptions> options)
	{
		var path = options.Value.Path;
		var assembly = ClrHelpers.ParseClrType(path);
		var container = Activator.CreateInstance(assembly.assembly, assembly.type)?.Unwrap();
		if (container is IAppContainer appContainer)
			_appContainer = appContainer;
		else
			throw new ArgumentException("Invalid application container");

	}

	public IAppContainer Container => _appContainer;
}

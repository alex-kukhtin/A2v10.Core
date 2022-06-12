// Copyright © 2022 Alex Kukhtin. All rights reserved.

using Microsoft.Extensions.Options;
using System.IO;
using System.Reflection;

namespace A2v10.Services;

public class AppProvider : IAppProvider
{
	private readonly IAppContainer _appContainer;
	public AppProvider(IOptions<AppOptions> options)
	{
		var path = options.Value.Path;
		//var path = "clr-type:MainApp.AppContainer;assembly=MainApp";
		var assembly = ClrHelpers.ParseClrType(path);
		var assPath = Assembly.GetExecutingAssembly().Location;
		var dir = Path.GetDirectoryName(assPath)!;
		var fname = Path.Combine(dir, $"{assembly.assembly}.dll");
		if (!File.Exists(fname))
			throw new FileNotFoundException(fname);
		var ass = Assembly.LoadFrom(fname);
		var container = Activator.CreateInstance(assembly.assembly, assembly.type)?.Unwrap();
		if (container is IAppContainer appContainer)
			_appContainer = appContainer;
		else
			throw new ArgumentException("Invalid application container");

	}

	public IAppContainer Container => _appContainer;
}

// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Services.Javascript;

namespace A2v10.Services;

public class InvokeCommandJavascript : IModelInvokeCommand
{
	private readonly IAppCodeProvider _appCodeProvider;
	private readonly JavaScriptEngine _engine;

	public InvokeCommandJavascript(IServiceProvider service)
	{
		_appCodeProvider = service.GetRequiredService<IAppCodeProvider>();
		_engine = new JavaScriptEngine(service);
	}

	#region IModelInvokeCommand
	public async Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
	{
		if (String.IsNullOrEmpty(command.File))
			throw new DataServiceException("'file' must be specified for the javascript command");
		String file = command.File.AddExtension("js");
		String pathToRead = Path.Combine(command.Path, file);	
		using var stream = _appCodeProvider.FileStreamRO(pathToRead) ??
			throw new DataServiceException($"Script not found '{file}'");
        using var sr = new StreamReader(stream);
		var text = await sr.ReadToEndAsync(); 
		if (String.IsNullOrEmpty(text))
			throw new DataServiceException($"Script is empty '{file}'");
		_engine.SetPath(command.Path);
		var result = _engine.Execute(text, parameters, command.Args ?? new ExpandoObject());
		return InvokeResult.JsonFromObject(result);
	}
	#endregion
}

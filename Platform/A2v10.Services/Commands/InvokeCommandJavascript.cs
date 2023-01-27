// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Services.Javascript;

namespace A2v10.Services;

public class InvokeCommandJavascript : IModelInvokeCommand
{
	private readonly IAppCodeProvider _appCodeProvider;
	private readonly ICurrentUser _currentUser;
	private readonly JavaScriptEngine _engine;

	public InvokeCommandJavascript(IServiceProvider service)
	{
		_appCodeProvider = service.GetRequiredService<IAppCodeProvider>();
		_currentUser = service.GetRequiredService<ICurrentUser>();
		_engine = new JavaScriptEngine(service);
	}

	#region IModelInvokeCommand
	public async Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
	{
		if (String.IsNullOrEmpty(command.File))
			throw new DataServiceException("'file' must be specified for the javascript command");
		String file = command.File.AddExtension("js");
		var text = await _appCodeProvider.ReadTextFileAsync(command.Path, file, _currentUser.IsAdminApplication);
		if (text == null)
			throw new DataServiceException($"Script not found '{file}'");
		_engine.SetPath(command.Path);
		var result = _engine.Execute(text, parameters, command.Args ?? new ExpandoObject());
		return InvokeResult.JsonFromObject(result);
	}
	#endregion
}

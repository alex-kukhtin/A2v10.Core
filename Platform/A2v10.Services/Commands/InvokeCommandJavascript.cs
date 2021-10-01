// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

using A2v10.Services.Javascript;
using Newtonsoft.Json;

namespace A2v10.Services
{
	public class InvokeCommandJavascript : IModelInvokeCommand
	{
		private readonly IAppCodeProvider _appCodeProvider;
		private readonly ICurrentUser _currentUser;
		private readonly JavaScriptEngine _engine;

		public InvokeCommandJavascript(IServiceProvider service)
		{
			_appCodeProvider = service.GetService<IAppCodeProvider>();
			_currentUser = service.GetService<ICurrentUser>();
			_engine = new JavaScriptEngine(service);
		}

		#region IModelInvokeCommand
		public async Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
		{
			if (String.IsNullOrEmpty(command.File))
				throw new DataServiceException("'file' must be specified for the javascript command");
			String file = "server.module".AddExtension("js");
			var text = await _appCodeProvider.ReadTextFileAsync(command.Path, file, _currentUser.IsAdminApplication);
			var result = _engine.Execute(text, parameters, command.Args);
			return InvokeResult.JsonFromObject(result);
		}
		#endregion
	}
}

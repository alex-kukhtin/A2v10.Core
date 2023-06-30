// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.


using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;

namespace A2v10.Services;
public static class ServerCommandRegistry
{
	public static IModelInvokeCommand GetCommand(ModelCommandType command, IServiceProvider serviceProvider)
	{
		return command switch {
			ModelCommandType.sql => new InvokeCommandExecuteSql(
					serviceProvider.GetRequiredService<IDbContext>()
				),
			ModelCommandType.invokeTarget => new InvokeCommandInvokeTarget(serviceProvider),
			ModelCommandType.clr => new InvokeCommandInvokeClr(serviceProvider),
			ModelCommandType.javascript => new InvokeCommandJavascript(serviceProvider),
			ModelCommandType.file => new InvokeCommandFile(serviceProvider),
			ModelCommandType.xml => throw new DataServiceException("xml command yet not implemented"),
			ModelCommandType.callApi => throw new DataServiceException("callApi command yet not implemented"),
			ModelCommandType.sendMessage => throw new DataServiceException("sendMessage command yet not implemented"),
			// new
			ModelCommandType.csharp => new InvokeCommandCSharp(serviceProvider),
			ModelCommandType.signal => new InvokeCommandSignal(serviceProvider),
			// deprectated
			ModelCommandType.startProcess or ModelCommandType.resumeProcess => throw new DataServiceException("Workflow commands are not supported in this version"),
			ModelCommandType.script => throw new DataServiceException("script command is not supported"),
			// other
			_ => throw new DataServiceException($"Server command for '{command}' not found")
		};
	}
}


// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Services
{
	public static class ServerCommandRegistry
	{
		public static IModelInvokeCommand GetCommand(ModelCommandType command, IServiceProvider serviceProvider)
		{
			return command switch {
				ModelCommandType.sql => new InvokeCommandExecuteSql(
						serviceProvider.GetService<IDbContext>()
					),
				ModelCommandType.invokeTarget => new InvokeCommandInvokeTarget(serviceProvider),
				ModelCommandType.clr => throw new DataServiceException("CLR yet not implemented"),
				ModelCommandType.javascript => new InvokeCommandJavascript(serviceProvider),
				ModelCommandType.file => throw new DataServiceException("file command yet not implemented"),
				ModelCommandType.xml => throw new DataServiceException("xml command yet not implemented"),
				ModelCommandType.callApi => throw new DataServiceException("callApi command yet not implemented"),
				// deprectated
				ModelCommandType.startProcess or ModelCommandType.resumeProcess => throw new DataServiceException("Workflow commands are not supported in this version"),
				ModelCommandType.script => throw new DataServiceException("script command is not supported"),
				_ => throw new DataServiceException("Server command for '{command}' not found")
			};
		}
	}
}

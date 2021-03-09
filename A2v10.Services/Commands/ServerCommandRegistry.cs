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
				ModelCommandType.clr => throw new DataServiceException("CLR not implemented"),
				_ => throw new DataServiceException("Server command for '{command}' not found")
			};
		}
	}
}

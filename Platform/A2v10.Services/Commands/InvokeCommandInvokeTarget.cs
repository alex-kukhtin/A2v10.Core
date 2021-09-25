// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using Newtonsoft.Json;
using System.Text;

namespace A2v10.Services
{
	public class InvokeCommandInvokeTarget : IModelInvokeCommand
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly IInvokeEngineProvider _engineProvider;
		private readonly ICurrentUser _currentUser;

		public InvokeCommandInvokeTarget(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
			_engineProvider = _serviceProvider.GetService<IInvokeEngineProvider>();
			_currentUser = _serviceProvider.GetService<ICurrentUser>();
		}

		public async Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
		{
			var target = command.Target.Split('.');
			if (target.Length != 2)
				throw new InvalidOperationException($"Invalid target: {command.Target}");
			var engine = _engineProvider.FindEngine(target[0]);
			if (engine == null)
				throw new InvalidOperationException($"InvokeTarget '{target[0]}' not found");
			try
			{
				var res = await engine.InvokeAsync(target[1], parameters);
				return new InvokeResult()
				{
					Body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(res)),
					ContentType = MimeTypes.Application.Json
				};
			} 
			catch (Exception ex)
			{
				throw new InvalidOperationException(ex.Message, ex);
			}
		}
	}
}

// Copyright © 2021-2025 Oleksandr Kukhtin. All rights reserved.

using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Microsoft.Extensions.DependencyInjection;

namespace A2v10.Services;

public class InvokeCommandInvokeTarget : IModelInvokeCommand
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IInvokeEngineProvider _engineProvider;
	private readonly ISignalSender _signalSender;

	public InvokeCommandInvokeTarget(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
		_engineProvider = _serviceProvider.GetRequiredService<IInvokeEngineProvider>();
		_signalSender = _serviceProvider.GetRequiredService<ISignalSender>();

    }

	public async Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
	{
		if (command.Target == null)
			throw new InvalidOperationException("Command.Target is null");
		var target = command.Target.Split('.');
		if (target.Length != 2)
			throw new InvalidOperationException($"Invalid target: {command.Target}");
		var engine = _engineProvider.FindEngine(target[0]) 
			?? throw new InvalidOperationException($"InvokeTarget '{target[0]}' not found");
        try
        {
			var res = await engine.InvokeAsync(target[1], parameters);
			if (command.Signal)
			{
				var signal = res.Eval<List<ExpandoObject>>("Signal");
				if (signal != null && signal.Count > 0)
				{
					foreach (var e in signal)
						await _signalSender.SendAsync(SignalResult.FromData(e));
				}
			}
            return new InvokeResult(
				body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(res)),
				contentType: MimeTypes.Application.Json
			);
		} 
		catch (Exception ex)
		{
			throw new InvalidOperationException(ex.Message, ex);
		}
	}
}

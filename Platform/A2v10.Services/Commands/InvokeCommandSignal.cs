// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;


namespace A2v10.Services;

public class InvokeCommandSignal : IModelInvokeCommand
{
	private readonly IServiceProvider _serivceProvider;
	private readonly ICurrentUser _currentUser;

	public InvokeCommandSignal(IServiceProvider services)
	{
		_serivceProvider = services;
		_currentUser = services.GetRequiredService<ICurrentUser>();
	}

	#region IModelInvokeCommand
	public Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
	{
		var result = InvokeResult.JsonFromObject(new ExpandoObject());
		result.Signal = SignalResult.FromExpando(parameters);
		return Task.FromResult<IInvokeResult>(result);
	}
	#endregion
}

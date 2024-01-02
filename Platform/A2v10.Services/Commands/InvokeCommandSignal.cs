// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;


namespace A2v10.Services;

#pragma warning disable CS9113 // Parameter is unread.
public class InvokeCommandSignal(IServiceProvider services) : IModelInvokeCommand
#pragma warning restore CS9113 // Parameter is unread.
{
	//private readonly IServiceProvider _serivceProvider = services;
	//private readonly ICurrentUser _currentUser = services.GetRequiredService<ICurrentUser>();

    #region IModelInvokeCommand
    public Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
	{
		var result = InvokeResult.JsonFromObject(new ExpandoObject());
		result.Signal = SignalResult.FromExpando(parameters);
		return Task.FromResult<IInvokeResult>(result);
	}
	#endregion
}

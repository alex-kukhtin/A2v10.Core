// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

namespace A2v10.Services;

public class InvokeCommandCSharp : IModelInvokeCommand
{
    private readonly IServiceProvider _serivceProvider;
    private readonly ICurrentUser _currentUser;

    public InvokeCommandCSharp(IServiceProvider services)
    {
        _serivceProvider = services;
        _currentUser = services.GetRequiredService<ICurrentUser>();
    }

    #region IModelInvokeCommand
    public Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
    {
        throw new NotImplementedException();
    }
    #endregion
}

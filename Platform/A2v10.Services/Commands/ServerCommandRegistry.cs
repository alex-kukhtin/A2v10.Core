// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;

namespace A2v10.Services;
public static class ServerCommandRegistry
{
    public static IModelInvokeCommand GetCommand(ModelCommandType command, IServiceProvider serviceProvider)
    {
        return command switch
        {
            ModelCommandType.sql => new InvokeCommandExecuteSql(
                    serviceProvider.GetRequiredService<IDbContext>()
                ),
            ModelCommandType.invokeTarget => new InvokeCommandInvokeTarget(serviceProvider),
            ModelCommandType.clr => throw new DataServiceException("CLR yet not implemented"),
            ModelCommandType.javascript => new InvokeCommandJavascript(serviceProvider),
            ModelCommandType.file => new InvokeCommandFile(serviceProvider),
            ModelCommandType.xml => throw new DataServiceException("xml command yet not implemented"),
            ModelCommandType.callApi => throw new DataServiceException("callApi command yet not implemented"),
            ModelCommandType.sendMessage => throw new DataServiceException("sendMessage command yet not implemented"),
            // new
            ModelCommandType.csharp => new InvokeCommandCSharp(serviceProvider),
            // deprectated
            ModelCommandType.startProcess or ModelCommandType.resumeProcess => throw new DataServiceException("Workflow commands are not supported in this version"),
            ModelCommandType.script => throw new DataServiceException("script command is not supported"),
            // other
            _ => throw new DataServiceException($"Server command for '{command}' not found")
        };
    }
}


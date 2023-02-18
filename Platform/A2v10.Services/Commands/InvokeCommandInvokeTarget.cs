﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public InvokeCommandInvokeTarget(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _engineProvider = _serviceProvider.GetRequiredService<IInvokeEngineProvider>();
        }

        public async Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
        {
            if (command.Target == null)
                throw new InvalidOperationException("Command.Target is null");
            var target = command.Target.Split('.');
            if (target.Length != 2)
                throw new InvalidOperationException($"Invalid target: {command.Target}");
            var engine = _engineProvider.FindEngine(target[0]);
            if (engine == null)
                throw new InvalidOperationException($"InvokeTarget '{target[0]}' not found");
            try
            {
                var res = await engine.InvokeAsync(target[1], parameters);
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
}

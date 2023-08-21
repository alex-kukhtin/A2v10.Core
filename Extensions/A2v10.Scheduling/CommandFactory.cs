// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Scheduling;

public class AddSchedulerHandlerFactory
{
    private readonly Dictionary<String, Type> _commands = new();
    private readonly Dictionary<String, Type> _handlers = new();

    public IDictionary<String, Type> Commands => _commands;
    public AddSchedulerHandlerFactory RegisterCommand<T>(String name)
    {
        _commands.Add(name, typeof(T));
        return this;
    }

    public AddSchedulerHandlerFactory RegisterJobHandler<T>(String name)
    {
        _handlers.Add(name, typeof(T));
        return this;
    }

    public Type FindHandler(String name)
    {
        if (_handlers.TryGetValue(name, out Type? handler) &&  handler != null) 
            return handler;
        throw new InvalidOperationException($"Handler {name} not found");
    }
}

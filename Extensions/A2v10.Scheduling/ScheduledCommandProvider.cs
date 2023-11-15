// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Scheduling;

public class ScheduledCommandProvider(IDictionary<String, Type> commands)
{
    private readonly IDictionary<String, Type> _commands = commands;

    public Type FindCommand(String commandName)
    {
        if (_commands.TryGetValue(commandName, out var command))
            return command;
        throw new InvalidOperationException($"Command handler for command '{commandName}' not found");
    }
}

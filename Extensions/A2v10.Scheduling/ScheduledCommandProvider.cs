
using System;
using System.Collections.Generic;

namespace A2v10.Scheduling;

public class ScheduledCommandProvider
{
    private readonly IDictionary<String, Type> _commands;
    public ScheduledCommandProvider(IDictionary<String, Type> commands)
    {
        _commands = commands;
    }

    public Type FindCommand(String commandName)
    {
        if (_commands.TryGetValue(commandName, out var command))
            return command;
        throw new InvalidOperationException($"Command handler for command '{commandName}' not found");
    }
}

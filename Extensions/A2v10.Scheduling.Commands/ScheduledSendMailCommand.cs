// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using A2v10.Infrastructure;
using A2v10.Scheduling.Infrastructure;

namespace A2v10.Scheduling.Commands;

public class ScheduledSendMailCommand(ILogger<ScheduledSendMailCommand> _logger, IMailService _mailService) : IScheduledCommand
{
    public Task ExecuteAsync(String? Data)
    {
        _logger.LogInformation("ScheduledSendMail at {Time}, Data = {Data}", DateTime.Now, Data);
        if (Data == null || String.IsNullOrEmpty("Data"))
            throw new InvalidOperationException("ScheduledSendMail. Data is empty");

        var msg = JsonConvert.DeserializeObject<ExpandoObject>(Data)
            ?? throw new InvalidOperationException("ScheduledSendMail. Invalid json");

        String toAddress = msg.Get<String>("To")
            ?? throw new InvalidOperationException("ScheduledSendMail. To is null");

        return _mailService.SendAsync(toAddress,
            msg.Get<String>("Subject") ?? String.Empty,
            msg.Get<String>("Body") ?? String.Empty);
    }
}

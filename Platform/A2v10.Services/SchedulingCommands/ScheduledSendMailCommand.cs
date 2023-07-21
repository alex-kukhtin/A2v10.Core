// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using A2v10.Scheduling.Infrastructure;

namespace A2v10.Services;

public class ScheduledSendMailCommand : IScheduledCommand
{
    private readonly ILogger<ScheduledSendMailCommand> _logger;
    private readonly IMailService _mailService;
    public ScheduledSendMailCommand(ILogger<ScheduledSendMailCommand> logger, IMailService mailService)
    {
        _logger = logger;
        _mailService = mailService; 
    }
    public Task ExecuteAsync(String? Data)
    {
        _logger.LogInformation("ScheduledSendMail at {Time}, Data = {ds}", DateTime.Now, Data);
        return Task.CompletedTask;
    }
}

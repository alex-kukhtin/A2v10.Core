// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;

using A2v10.Infrastructure;


namespace A2v10.Platform.Web;

public class SignalSender(IHubContext<DefaultHub> hubContext) : ISignalSender
{
    private readonly IHubContext<DefaultHub> _hubContext = hubContext;

    public Task SendAsync(ISignalResult message)
    {
        return _hubContext.SignalAsync(message);
    }
}


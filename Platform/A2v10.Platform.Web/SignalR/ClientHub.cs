// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using Microsoft.AspNetCore.SignalR;

using System.Threading.Tasks;

namespace A2v10.Platform.Web;

public class ClientHub : Hub
{
    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("message");
    }
}

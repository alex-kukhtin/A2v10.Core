// Copyright © 2020-2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

public class DefaultHub : Hub
{
	public override Task OnConnectedAsync()
	{
		return base.OnConnectedAsync();
	}
}

public static class DefaultHubExtensions
{
	public static Task SignalAsync(this IHubContext<DefaultHub> hub, Int64 userId, String message, ExpandoObject? prms = null)
	{
		if (userId == -1)
            return hub.Clients.All.SendAsync("signal", message, prms);
		else
			return hub.Clients.User(userId.ToString()).SendAsync("signal", message, prms);
	}

	public static Task SignalAsync(this IHubContext<DefaultHub> hub, ISignalResult? signal)
	{
		if (signal == null)
			return Task.CompletedTask;
		if (signal.UserId == -1)
			return hub.Clients.All.SendAsync("signal", signal.Message, signal.Data);
		else
			return hub.Clients.User(signal.UserId.ToString()).SendAsync("signal", signal.Message, signal.Data);
	}
}

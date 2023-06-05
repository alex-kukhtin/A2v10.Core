// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Dynamic;
using System.Threading.Tasks;

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
		return hub.Clients.User(userId.ToString()).SendAsync("signal", message, prms);
	}

	public static Task SignalAsync(this IHubContext<DefaultHub> hub, ISignalResult? signal)
	{
		if (signal == null)
			return Task.CompletedTask;
		return hub.Clients.User(signal.UserId.ToString()).SendAsync("signal", signal.Message, signal.Data);
	}
}

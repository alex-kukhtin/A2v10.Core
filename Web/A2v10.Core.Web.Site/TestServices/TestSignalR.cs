
using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace A2v10.Core.Web.Site;

public class TSignalR : IClrInvokeTarget
{
    private readonly ICurrentUser _currentUser;
    private readonly ISignalSender _signalSender;

    public TSignalR(IServiceProvider serviceProvider)
    {
		_currentUser =  serviceProvider.GetRequiredService<ICurrentUser>();
        _signalSender = serviceProvider.GetRequiredService<ISignalSender>();

    }

    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        var userId = _currentUser.Identity.Id
            ?? throw new InvalidOperationException("UserId is null");

        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            await _signalSender.SendAsync(new SignalResult(userId, "TEST", args));
        }

        return new ExpandoObject();
    }
}

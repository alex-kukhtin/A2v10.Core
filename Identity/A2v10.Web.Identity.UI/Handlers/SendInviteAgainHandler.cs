// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Identity.UI;

public class SendInviteAgainHandler : IClrInvokeTarget
{
    private readonly EmailSender _emailSender;
    public SendInviteAgainHandler(IServiceProvider serviceProvider)
    {
        _emailSender = new EmailSender(serviceProvider);
	}

    public async Task<object> InvokeAsync(ExpandoObject args)
    {
        var userId = args.Eval<Int64>("User.Id");

        if (userId == 0)
			throw new InvalidOperationException("UserId is null");
        await _emailSender.SendInviteAgain(userId);

        return new ExpandoObject()
        {
            {"Success", true }
        };
    }
}

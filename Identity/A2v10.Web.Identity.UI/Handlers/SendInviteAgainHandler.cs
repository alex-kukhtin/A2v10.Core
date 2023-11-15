// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Identity.UI;

public class SendInviteAgainHandler(IServiceProvider serviceProvider) : IClrInvokeTarget
{
    private readonly EmailSender _emailSender = new(serviceProvider);

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

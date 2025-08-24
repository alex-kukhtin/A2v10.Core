// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Identity.UI;

public class SetLocaleHandler(IServiceProvider serviceProvider) : IClrInvokeTarget
{
    private readonly UserManager<AppUser<Int64>> _userManager = serviceProvider.GetRequiredService<UserManager<AppUser<Int64>>>();
    private readonly SignInManager<AppUser<Int64>> _signInManager = serviceProvider.GetRequiredService<SignInManager<AppUser<Int64>>>();
    private readonly ICurrentUser _currentUser = serviceProvider.GetRequiredService<ICurrentUser>();

    public async Task<object> InvokeAsync(ExpandoObject args)
    {
        var userId = _currentUser.Identity.Id ??
            throw new InvalidOperationException("UserId is null");
        
        var locale = args.Get<String>("Locale")
			?? throw new InvalidOperationException("Locale is null");

		var user = await _userManager.FindByIdAsync(userId.ToString())
			?? throw new InvalidOperationException("UserId not found");

        user.Locale = locale;
        user.Flags |= UpdateFlags.Locale;
        await _userManager.UpdateAsync(user);

        await _signInManager.RefreshSignInAsync(user);

        return new ExpandoObject();
    }
}

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

public class SetPasswordHandler(IServiceProvider serviceProvider) : IClrInvokeTarget
{
    private readonly UserManager<AppUser<Int64>> _userManager = serviceProvider.GetRequiredService<UserManager<AppUser<Int64>>>();

    public async Task<object> InvokeAsync(ExpandoObject args)
    {
		var src = args.Get<ExpandoObject>("User")
			?? throw new InvalidOperationException("User is null");

        var id = src.Get<Int64>("Id");
        if (id == 0)
			throw new InvalidOperationException("UserId is null");

        var pwd = src.Get<String>("Password"); 
        if (String.IsNullOrEmpty(pwd))
			throw new InvalidOperationException("Password is null");

		var user = await _userManager.FindByIdAsync(id.ToString())
			?? throw new InvalidOperationException("UserId not found");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, pwd);

        if (!result.Succeeded) {
            var msg = String.Join(",", result.Errors.Select(e => e.Code));
			return new ExpandoObject()
		    {
			    { "Success", false },
			    { "Error",  msg }
		    };
		}

		return new ExpandoObject()
        {
            {"Success", true },
            {"User", new ExpandoObject() {
                    { "Id", user.Id },
                    { "UserName", user.UserName ?? String.Empty }
                }
            }
        };
    }
}

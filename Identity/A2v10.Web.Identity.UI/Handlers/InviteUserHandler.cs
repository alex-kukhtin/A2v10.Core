// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using A2v10.Infrastructure;
using A2v10.Web.Identity;
using A2v10.Data.Interfaces;

namespace A2v10.Identity.UI;

public class InviteUserHandler : IClrInvokeTarget
{
    private readonly UserManager<AppUser<Int64>> _userManager;
    private readonly AppUserStoreOptions<Int64> _userStoreOptions;
    private readonly IDbContext _dbContext;
    private readonly EmailSender _emailSender;
    public InviteUserHandler(IServiceProvider serviceProvider)
    {
        _userManager = serviceProvider.GetRequiredService<UserManager<AppUser<Int64>>>();
        _userStoreOptions = serviceProvider.GetRequiredService<IOptions<AppUserStoreOptions<Int64>>>().Value;
        _dbContext = serviceProvider.GetRequiredService<IDbContext>();
        _emailSender = new EmailSender(serviceProvider);
	}

    Boolean IsMultiTenant => _userStoreOptions.MultiTenant ?? false;

    public async Task<object> InvokeAsync(ExpandoObject args)
    {
        var userName = args.Eval<String>("User.UserName") ??
            throw new InvalidOperationException("User.UserName is null");
        var user = new AppUser<Int64>()
        {
            UserName = userName,
            Email = userName,
            Roles = args.Eval<String>("User.Roles")
	    };

        if (IsMultiTenant)
            user.Tenant = args.Get<Int32>("TenantId");

        var identityResult = await _userManager.CreateAsync(user);
        if (!identityResult.Succeeded)
            return Error(String.Join(",", identityResult.Errors.Select(e => e.Code)));

        if (IsMultiTenant) {
            // create tenant user
            var createdUser = await _userManager.FindByIdAsync(user.Id.ToString())
                ?? throw new InvalidOperationException("Create user failed");
            await _dbContext.ExecuteAsync<AppUser<Int64>>(user.Segment, "a2security.[User.Invite]", createdUser);
        }

        await _emailSender.SendInviteEMail(user);

        return new ExpandoObject()
        {
            {"Success", true },
            {"User", new ExpandoObject() {
                    { "Id", user.Id },
                    { "UserName", user.UserName },
                    { "Email", user.Email },
                }
            },
        };
    }

    static ExpandoObject Error(String error)
    {
        return new ExpandoObject()
        {
            { "Success", false },
            { "Error",  error }
        };
    }
}

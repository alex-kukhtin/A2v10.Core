// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;

using A2v10.Infrastructure;
using A2v10.Web.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace A2v10.Identity.UI;


public class CreateUserHandler : IClrInvokeTarget
{
    private readonly AppUserStoreOptions<Int64> _userStoreOptions;
    private readonly UserManager<AppUser<Int64>> _userManager;
    public CreateUserHandler(IServiceProvider serviceProvider)
    {
        _userStoreOptions = serviceProvider.GetRequiredService<IOptions<AppUserStoreOptions<Int64>>>().Value;
        _userManager = serviceProvider.GetRequiredService<UserManager<AppUser<Int64>>>();
    }
    Boolean IsMultiTenant => _userStoreOptions.MultiTenant ?? false;

    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        Int32? TenantId = IsMultiTenant ? args.Get<Int32>("TenantId") : null;

        var user = new AppUser<Int64>()
        {
            Tenant = TenantId,
            UserName = args.GetNotNull<String>("UserName"),
            PersonName = args.Get<String>("PersonName"),
            Email = args.GetNotNull<String>("UserName"),
            PhoneNumber = args.Get<String>("PhoneNumber"),
            Memo = args.Get<String>("Memo")
        };

        var password = args.GetNotNull<String>("Password");  
        var result = await _userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _userManager.ConfirmEmailAsync(user, token);
            var createdUser = await _userManager.FindByIdAsync(user.Id.ToString())
                ?? throw new InvalidOperationException("Create user failed");
            return createdUser;
        }
        throw new InvalidOperationException(String.Join(", ", result.Errors.Select(x => x.Description)));
    }
}

// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;

using A2v10.Infrastructure;
using A2v10.Web.Identity;
using A2v10.Data.Interfaces;


namespace A2v10.Identity.UI;

public class CreateUserHandler(IServiceProvider serviceProvider) : IClrInvokeTarget
{
    private readonly AppUserStoreOptions<Int64> _userStoreOptions = serviceProvider.GetRequiredService<IOptions<AppUserStoreOptions<Int64>>>().Value;
    private readonly UserManager<AppUser<Int64>> _userManager = serviceProvider.GetRequiredService<UserManager<AppUser<Int64>>>();
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();

    Boolean IsMultiTenant => _userStoreOptions.MultiTenant ?? false;

    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        Int32? TenantId = IsMultiTenant ? args.Get<Int32>("TenantId") : null;

        var src = args.Get<ExpandoObject>("User")
            ?? throw new InvalidOperationException("User is null");

        var user = new AppUser<Int64>()
        {
            Tenant = TenantId,
            UserName = src.GetNotNull<String>("UserName"),
            PersonName = src.Get<String>("PersonName"),
            Email = src.GetNotNull<String>("Email"),
            PhoneNumber = src.Get<String>("PhoneNumber"),
            Memo = src.Get<String>("Memo")
        };

        user.Email ??= user.UserName;

        var password = src.GetNotNull<String>("Password");  
        var result = await _userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {

			var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _userManager.ConfirmEmailAsync(user, token);

            var createdUser = await _userManager.FindByIdAsync(user.Id.ToString())
                ?? throw new InvalidOperationException("Create user failed");

            // TODO: перенести это в _tenantManager
			if (IsMultiTenant)
				await _dbContext.ExecuteAsync<AppUser<Int64>>(user.Segment, $"[{_userStoreOptions.SecuritySchema}].[User.Invite]", createdUser);

			var cu = new ExpandoObject()
            {
                { "Id", createdUser.Id },
                { "EmailConfirmed", createdUser.EmailConfirmed }
		    };
        
            cu.SetNotNull("UserName", createdUser.UserName);
			cu.SetNotNull("Email", createdUser.Email);
			cu.SetNotNull("PersonName", createdUser.PersonName);
			cu.SetNotNull("Memo", createdUser.Memo);
			cu.SetNotNull("Locale", createdUser.Locale);

            return new ExpandoObject()
            {
                {"Success", true },
                {"User", cu }
            };
		}
		throw new InvalidOperationException(String.Join(", ", result.Errors.Select(x => x.Description)));
    }
}

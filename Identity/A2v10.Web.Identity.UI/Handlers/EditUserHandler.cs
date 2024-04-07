// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Identity.UI;

public class EditUserHandler(IServiceProvider serviceProvider) : IClrInvokeTarget
{
    private readonly AppUserStoreOptions<Int64> _userStoreOptions = serviceProvider.GetRequiredService<IOptions<AppUserStoreOptions<Int64>>>().Value;
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();
    private readonly ICurrentUser _currentUser = serviceProvider.GetRequiredService<ICurrentUser>();

    Boolean IsMultiTenant => _userStoreOptions.MultiTenant ?? false;

    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        Int32? tenantId = IsMultiTenant ? _currentUser.Identity.Tenant : null;
        Int64? userId = _currentUser.Identity.Id ??
            throw new InvalidOperationException("CurrentUser is null");

        Int64 id = args.Get<Int64>("Id");
        if (id == 0)
            throw new InvalidOperationException("Id is null");

        var editPrms = new EditUserParams()
        {
            Id = id,
            UserId = userId.Value,
            TenantId = tenantId,
            PersonName = args.Get<String>("PersonName"),
            PhoneNumber = args.Get<String>("PhoneNumber"),
            Memo = args.Get<String>("Memo")
        };

        var editedUser = await _dbContext.ExecuteAndLoadAsync<EditUserParams, AppUser<Int64>>(
                _userStoreOptions.DataSource, $"[{_userStoreOptions.SecuritySchema}].[User.EditUser]", editPrms)
            ?? throw new InvalidOperationException("Error editing user");

        if (IsMultiTenant)
            await _dbContext.ExecuteAsync(editedUser.Segment, $"[{_userStoreOptions.SecuritySchema}].[User.Tenant.EditUser]", editedUser);

        return new ExpandoObject()
        {
            {"Success", true },
        };
    }
}

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

public class DeleteUserHandler(IServiceProvider serviceProvider) : IClrInvokeTarget
{
    private readonly AppUserStoreOptions<Int64> _userStoreOptions = serviceProvider.GetRequiredService<IOptions<AppUserStoreOptions<Int64>>>().Value;
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();

    Boolean IsMultiTenant => _userStoreOptions.MultiTenant ?? false;

    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        Int32? TenantId = IsMultiTenant ? args.Get<Int32>("TenantId") : null;
        Int64 Id = args.Get<Int64>("Id");
        if (Id == 0)
            throw new InvalidOperationException("Id is null");

        var deletePrms = new DeleteUserParams()
        {
            UserId = args.Get<Int64>("UserId"),
            TenantId = TenantId,
            Id = Id
        };

        var deletedUser = await _dbContext.ExecuteAndLoadAsync<DeleteUserParams, AppUser<Int64>>(
            _userStoreOptions.DataSource, $"[{_userStoreOptions.SecuritySchema}].[User.DeleteUser]", deletePrms)
            ?? throw new InvalidOperationException("Error deleting user");

        if (IsMultiTenant)
        {
            var delUser = new ExpandoObject()
            {
                {"Id", deletedUser.Id},
                {"TenantId", TenantId },
                {"UserName", deletedUser.UserName},
                {"Email", deletedUser.Email },
                {"PhoneNumber", deletedUser.PhoneNumber }
            };
			await _dbContext.ExecuteExpandoAsync(deletedUser.Segment, $"[{_userStoreOptions.SecuritySchema}].[User.Tenant.DeleteUser]", delUser);
        }

        return new ExpandoObject()
        {
            {"Success", true },
        };
    }
}

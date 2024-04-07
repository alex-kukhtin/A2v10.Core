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

public record CreateUserParams
{
    public Int64 UserId { get; init; } 
    public Int32? TenantId { get; init; }
    public String? ApiKey { get; init; }
    public String? Name { get; init; }
	public String? PersonName { get; init; }
    public String? PhoneNumber { get; init; }
    public String? Memo { get; init; }
}

public record EditUserParams : CreateUserParams
{
    public Int64 Id { get; init; }
}

public class CreateApiUserHandler(IServiceProvider serviceProvider) : IClrInvokeTarget
{
    private readonly AppUserStoreOptions<Int64> _userStoreOptions = serviceProvider.GetRequiredService<IOptions<AppUserStoreOptions<Int64>>>().Value;
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();

    Boolean IsMultiTenant => _userStoreOptions.MultiTenant ?? false;

    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        Int32? TenantId = IsMultiTenant ? args.Get<Int32>("TenantId") : null;

        var apiKey = HandlerHelpers.GenerateApiKey();

        var userName = Guid.NewGuid().ToString();

        var createPrms = new CreateUserParams()
        {
            UserId = args.Get<Int64>("UserId"),
            TenantId = TenantId,
            ApiKey = apiKey,
            Name = userName,
			PersonName = args.Get<String>("PersonName"),
			Memo = args.Get<String>("Memo")
        };

        var createdUser = await _dbContext.ExecuteAndLoadAsync<CreateUserParams, AppUser<Int64>>(
                _userStoreOptions.DataSource, $"[{_userStoreOptions.SecuritySchema}].[User.CreateApiUser]", createPrms)
            ?? throw new InvalidOperationException("Error creating API user");

        if (IsMultiTenant)
            await _dbContext.ExecuteAsync(createdUser.Segment, $"[{_userStoreOptions.SecuritySchema}].[User.Tenant.CreateApiUser]", createdUser);

        return createdUser;
    }
}

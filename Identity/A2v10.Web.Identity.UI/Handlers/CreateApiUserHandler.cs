// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Web.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace A2v10.Identity.UI;

public record CreateUserParams
{
    public Int64 UserId { get; init; } 
    public Int32? TenantId { get; init; }
    public String? ApiKey { get; init; }
    public String? Name { get; init; }
    public String? Memo { get; init; }
}

public class CreateApiUserHandler : IClrInvokeTarget
{
    private readonly AppUserStoreOptions<Int64> _userStoreOptions;
    private readonly IDbContext _dbContext;
    public CreateApiUserHandler(IServiceProvider serviceProvider)
    {
        _userStoreOptions = serviceProvider.GetRequiredService<IOptions<AppUserStoreOptions<Int64>>>().Value;
        _dbContext = serviceProvider.GetRequiredService<IDbContext>();
    }
    Boolean IsMultiTenant => _userStoreOptions.MultiTenant ?? false;

    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        Int32? TenantId = IsMultiTenant ? args.Get<Int32>("TenantId") : null;

        var apiKey = HandlerHelpers.GenerateApiKey();

        var createPrms = new CreateUserParams()
        {
            UserId = args.Get<Int64>("UserId"),
            TenantId = TenantId,
            ApiKey = apiKey,
            Name = args.Get<String>("Name"),    
            Memo = args.Get<String>("Memo")
        };

        var createdUser = await _dbContext.ExecuteAndLoadAsync<CreateUserParams, AppUser<Int64>>(_userStoreOptions.DataSource, "a2security.[User.CreateApiUser]", createPrms)
            ?? throw new InvalidOperationException("Error creating API user");

        if (IsMultiTenant)
            await _dbContext.ExecuteAsync(createdUser.Segment, "a2security.[User.Tenant.CreateApiUser]", createdUser);

        return createdUser;
    }
}

// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Identity.UI;

public class InviteUserHandler : IClrInvokeTarget
{
    private readonly UserManager<AppUser<Int64>> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMailService _mailService;
    private readonly ILocalizer _localizer;
    private readonly AppUserStoreOptions<Int64> _userStoreOptions;
    public InviteUserHandler(IServiceProvider serviceProvider)
    {
        _userManager = serviceProvider.GetRequiredService<UserManager<AppUser<Int64>>>();
        _httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();   
        _mailService = serviceProvider.GetRequiredService<IMailService>();
        _localizer = serviceProvider.GetRequiredService<ILocalizer>();
        _userStoreOptions = serviceProvider.GetRequiredService<IOptions<AppUserStoreOptions<Int64>>>().Value;
    }

    public async Task<object> InvokeAsync(ExpandoObject args)
    {
        var userName = args.Eval<String>("User.UserName") ??
            throw new InvalidOperationException("User.UserName is null");
        var user = new AppUser<Int64>()
        {
            UserName = userName,
            Email = userName    
        };

        if (_userStoreOptions.MultiTenant ?? false)
            user.Tenant = args.Get<Int32>("TenantId");

        var identityResult = await _userManager.CreateAsync(user);
        if (!identityResult.Succeeded)
            return Error(String.Join(",", identityResult.Errors.Select(e => e.Code)));
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        var verified = await _userManager.VerifyUserTokenAsync(user, _userManager.Options.Tokens.EmailConfirmationTokenProvider, UserManager<AppUser<Int64>>.ConfirmEmailTokenPurpose, token);

        var emailLink = Url(userName, token);

        var subject = _localizer.Localize("@[InviteUserSubject]") ?? "Invitation";
        var body = _localizer.Localize("@[InviteUserBody]") ??
            $"<a href={0}>Click here to continue registeration</a>";
        body = body.Replace("{0}", emailLink);

        await _mailService.SendAsync(userName, subject, body);
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

    String Url(String user, String token)
    {
        var ctx = _httpContextAccessor.HttpContext ??
                throw new InvalidOperationException("HttpContext is null");
        var rq = ctx.Request;
        return $"{rq.Scheme}://{rq.Host}/account/invite?user={user}&token={WebUtility.UrlEncode(token)}";
    }
    ExpandoObject Error(String error)
    {
        return new ExpandoObject()
        {
            { "Success", false },
            { "Error",  error }
        };
    }
}

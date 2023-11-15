// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Identity.UI;

internal class EmailSender(IServiceProvider serviceProvider)
{
	private readonly UserManager<AppUser<Int64>> _userManager = serviceProvider.GetRequiredService<UserManager<AppUser<Int64>>>();
	private readonly IMailService _mailService = serviceProvider.GetRequiredService<IMailService>();
	private readonly ILocalizer _localizer = serviceProvider.GetRequiredService<ILocalizer>();
	private readonly IHttpContextAccessor _httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

    public async Task SendInviteEMail(AppUser<Int64> user)
	{
		var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

		// var verified = await _userManager.VerifyUserTokenAsync(user, _userManager.Options.Tokens.EmailConfirmationTokenProvider, UserManager<AppUser<Int64>>.ConfirmEmailTokenPurpose, token);
		var userName = user.UserName
			?? throw new InvalidOperationException("User name is null");

		var emailLink = Url(userName, token);

		// TODO: use EMailTextProvider
		var subject = _localizer.Localize("@[InviteUserSubject]") ?? "Invitation";
		var body = _localizer.Localize("@[InviteUserBody]") ??
			$"<a href={0}>Click here to continue registeration</a>";
		body = body.Replace("{0}", emailLink);

		await _mailService.SendAsync(userName, subject, body);
	}

	public async Task SendInviteAgain(Int64 userId)
	{
		var appUser = await _userManager.FindByIdAsync(userId.ToString());
		if (appUser == null || appUser.IsEmpty)
			throw new InvalidOperationException("User not found");
		await SendInviteEMail(appUser);
    }

	String Url(String user, String token)
	{
		var ctx = _httpContextAccessor.HttpContext ??
				throw new InvalidOperationException("HttpContext is null");
		var rq = ctx.Request;
		return $"{rq.Scheme}://{rq.Host}/account/invite?user={user}&token={WebUtility.UrlEncode(token)}";
	}
}

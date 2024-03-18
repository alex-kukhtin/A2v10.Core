// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Identity.UI;

public class TwoFactorGetQrcodeUriHandler(IServiceProvider serviceProvider) : IClrInvokeTarget
{
    private readonly UserManager<AppUser<Int64>> _userManager = serviceProvider.GetRequiredService<UserManager<AppUser<Int64>>>();
	private readonly UrlEncoder _urlEncoder = serviceProvider.GetRequiredService<UrlEncoder>();

	public async Task<object> InvokeAsync(ExpandoObject args)
    {
        var id = args.Get<Int64>("UserId");
        if (id == 0)
			throw new InvalidOperationException("UserId is null");

        var user = await _userManager.FindByIdAsync(id.ToString())
			?? throw new InvalidOperationException("User not found");

        if (user.UserName == null)
			throw new InvalidOperationException("UserName is null");

		var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (String.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
			key = await _userManager.GetAuthenticatorKeyAsync(user);

		}
		if (key == null)
			throw new InvalidOperationException("AuthenticatorKey is null");

		var qrCodeUri = GenerateQrCodeUri(user.UserName, key);

		return new ExpandoObject()
        {
            {"Success", true },
            {"Uri", qrCodeUri } 
        };
    }
	private String GenerateQrCodeUri(String email, String unformattedKey)
	{
		const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

		return String.Format(
		AuthenticatorUriFormat,
			_urlEncoder.Encode(_userManager.Options.Tokens.AuthenticatorIssuer),
			_urlEncoder.Encode(email),
			unformattedKey);
	}
}

public class TwoFactorSetAuthenticatorHandler(IServiceProvider serviceProvider) : IClrInvokeTarget
{
	private readonly UserManager<AppUser<Int64>> _userManager = serviceProvider.GetRequiredService<UserManager<AppUser<Int64>>>();
	public async Task<object> InvokeAsync(ExpandoObject args)
	{
		var id = args.Get<Int64>("UserId");
		if (id == 0)
			throw new InvalidOperationException("UserId is null");

		var user = await _userManager.FindByIdAsync(id.ToString())
			?? throw new InvalidOperationException("User not found");

		var code = args.Get<String>("Code");
		if (String.IsNullOrEmpty(code))
			throw new InvalidOperationException("Code is null");

		var twoFactorValid = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

		if (twoFactorValid)
		{
			if (!await _userManager.GetTwoFactorEnabledAsync(user))
				await _userManager.SetTwoFactorEnabledAsync(user, true);

			return new ExpandoObject()
			{
				{"Success", true }
			};
		}
		return new ExpandoObject()
			{
				{"Success", false },
				{ "Error",  "Invalid verification code" }
			};
	}
}


public class TwoFactorDisableHandler(IServiceProvider serviceProvider) : IClrInvokeTarget
{
	private readonly UserManager<AppUser<Int64>> _userManager = serviceProvider.GetRequiredService<UserManager<AppUser<Int64>>>();

	public async Task<object> InvokeAsync(ExpandoObject args)
	{
		var id = args.Get<Int64>("UserId");
		if (id == 0)
			throw new InvalidOperationException("UserId is null");

		var user = await _userManager.FindByIdAsync(id.ToString())
			?? throw new InvalidOperationException("User not found");

		await _userManager.SetTwoFactorEnabledAsync(user, false);

		return new ExpandoObject()
		{
			{"Success", true },
		};
	}
}

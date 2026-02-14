// Copyright © 2015-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Linq;
using System.Dynamic;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Identity.UI;

//TODO: remove LOCAL REDIRECT from REGISTER & INVITE POST actions
// like InitPassword 


[Route("account/[action]")]
[ApiExplorerSettings(IgnoreApi = true)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[AllowAnonymous]
public class AccountController(
	SignInManager<AppUser<Int64>> _signInManager, 
	UserManager<AppUser<Int64>> _userManager,
    IAntiforgery _antiforgery, IDbContext _dbContext,
    IApplicationTheme _appTheme, IMailService _mailService, ILocalizer _localizer,
    IConfiguration _configuration, IAppTenantManager _appTenantManager, 
	ILogger<AccountController> _logger, UrlEncoder _urlEncoder,
    IDataProtectionProvider protectionProvider,
	ICurrentUser _currentUser,
    IOptions<AppUserStoreOptions<Int64>> userStoreOptions) : Controller
{
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("Login");
	private readonly AppUserStoreOptions<Int64> _userStoreOptions = userStoreOptions.Value;

    private Boolean IsMultiTenant => _userStoreOptions.MultiTenant ?? false;
	private String? CatalogDataSource => _userStoreOptions.DataSource;
	private String? SecuritySchema => _userStoreOptions.SecuritySchema;


    void RemoveAllCookies()
	{
		foreach (var key in Request.Cookies.Keys)
			Response.Cookies.Delete(key);
	}
	void RemoveAntiforgeryCookie()
	{
		foreach (var key in Request.Cookies.Keys.Where(x => x.Contains("Antiforgery") || x.Contains("UserLocale")))
			Response.Cookies.Delete(key);
	}

	void RemoveNonAntiforgeryCookies()
	{
		foreach (var key in Request.Cookies.Keys.Where(x => !x.Contains("Antiforgery") && !x.Contains("UserLocale")))
			Response.Cookies.Delete(key);
	}

	private async Task<IActionResult?> SingleExternalAuthenticationSchemesAsync(String[]? splitted)
	{
		if (splitted == null)
			return null;
        var availableSchemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
        foreach (var scheme in splitted.Where(s => s != "Local"))
		{
			if (!availableSchemes.Any(s => s.Name == scheme))
				throw new InvalidOperationException($"External Authentication Scheme '{scheme}' not found");
		}
		if (splitted.Length == 1)
		{
			var scheme = splitted[0];
			if (availableSchemes.Any(s => s.Name == scheme))
			{
                var redirectUrl = Url.ActionLink("loginexternal");
                var loginInfo = _signInManager.ConfigureExternalAuthenticationProperties(scheme, redirectUrl);
                return new ChallengeResult(scheme, loginInfo);
            }
        }
		return null;
    }

	Task<AppTitleModel?> LoadTitleAsync()
	{
		return _dbContext.LoadAsync<AppTitleModel>(CatalogDataSource, "a2sys.[AppTitle.Load]");
	}

	[HttpGet]
	public async Task<IActionResult> OpenIdLogin(String provider)
	{
		var availableSchemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
		var scheme = availableSchemes.FirstOrDefault(x => x.Name == provider) 
			?? throw new InvalidOperationException($"Provider {provider} not found");
		var redirectUrl = Url.ActionLink("loginexternal");
		var loginInfo = _signInManager.ConfigureExternalAuthenticationProperties(scheme.Name, redirectUrl);
		return new ChallengeResult(scheme.Name, loginInfo);
	}

	[HttpGet]
	public async Task<IActionResult> Login(String returnUrl)
	{
		_logger.LogInformation("Login");

		RemoveNonAntiforgeryCookies();

		var providers = _configuration.GetValue<String>("Identity:Providers")?.Split(',');

		var external = await SingleExternalAuthenticationSchemesAsync(providers);
		if (external != null)
			return external;

        var m = new LoginViewModel()
		{
			Title = await LoadTitleAsync(),
			Theme = _appTheme.MakeTheme(),
			RequestToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken,
			LoginProviders = providers != null ? String.Join(',', providers) : "Local",
			ReturnUrl = returnUrl
		};
        return View(m);
	}

	/*
			* CHANGE PHONE NUMBER
		var user = await _userManager.FindByNameAsync(model.Login);
		user.PhoneNumber = "PHONENUMBER";
        var token = await _userManager.GenerateChangePhoneNumberTokenAsync(user, "PHONENUMBER");
		var verified = await _userManager.VerifyChangePhoneNumberTokenAsync(user, token, "PHONENUMBER");
		await _userManager.ChangePhoneNumberAsync(user, "PHONENUMBER", token);
		//
		/* refresh claims!
		var user = await _userManager.FindByNameAsync(model.Login);
		await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("Organization", "234563"));
		// sign in again!
		await _signInManager.SignInAsync(user);
			* LICENSE MANAGER
			* _licenseManager.LoadModules()	
	 */

	[HttpPost]
	public async Task<IActionResult> Login([FromForm] LoginViewModel model)
	{
		try
		{
			var isValid = await _antiforgery.IsRequestValidAsync(HttpContext);

			if (!isValid)
				return new JsonResult(JsonResponse.Error("AntiForgery"));

			if (model.Login == null || model.Password == null)
				return new JsonResult(JsonResponse.Error("Failed"));

			var user = await _userManager.FindByNameAsync(model.Login);
			if (user == null || user.IsEmpty)
				return new JsonResult(JsonResponse.Error("Failed"));
			if (!user.EmailConfirmed)
				return new JsonResult(JsonResponse.Error("EmailNotConfirmed"));

			var result = await _signInManager.PasswordSignInAsync(model.Login, model.Password, model.IsPersistent, lockoutOnFailure: true);
			if (result.Succeeded)
			{
				if (user.SetPassword)
				{
					var changePasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);
					var token = GetInitPasswordToken(user, changePasswordToken);
					return new JsonResult(JsonResponse.Redirect($"/account/initpassword?token={token}"));
				}

				var llprms = new ExpandoObject()
				{
					{ "Id", user.Id },
					{ "LastLoginHost", Request.Host.Host }
				};
                await _dbContext.ExecuteExpandoAsync(CatalogDataSource, $"[{SecuritySchema}].[UpdateUserLogin]", llprms);
				RemoveAntiforgeryCookie();
				var returnUrl = model.ReturnUrl;
				if (returnUrl == null || returnUrl.StartsWith("/account", StringComparison.OrdinalIgnoreCase))
					returnUrl = "/";
				return new JsonResult(JsonResponse.Redirect(returnUrl));
			}
			else if (result.RequiresTwoFactor)
			{
				return new JsonResult(JsonResponse.Redirect($"/account/twofactor?login={model.Login}&returnUrl={_urlEncoder.Encode(model.ReturnUrl ?? "/")}"));
			}
			else
			{
				return new JsonResult(JsonResponse.Error(result.ToString()));
			}
		} 
		catch (Exception ex) 
		{
            return new JsonResult(JsonResponse.Error(ex));
        }
    }


	[HttpGet]
	public async Task<IActionResult> Register()
	{
		RemoveNonAntiforgeryCookies();
		var m = new RegisterViewModel()
		{
			Title = await LoadTitleAsync(),
			Theme = _appTheme.MakeTheme(),
			RequestToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken
		};
		return View(m);
	}

	async Task SendConfirmCode(AppUser<Int64> user)
	{
		if (user.UserName == null)
			throw new InvalidOperationException();

		String emailConfirmCode = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
		String emailConfirmLink = await _userManager.GenerateEmailConfirmationTokenAsync(user);

		var token = _protector.Protect($"{user.Id}\t{emailConfirmLink}");

		var callbackUrl = Url.ActionLink("confirmemail", "account", new { token });

		String subject = _localizer.Localize("@[ConfirmEMail]") ??
			throw new InvalidOperationException("ConfirmEMail body not found");
		String body = _localizer.Localize("@[ConfirmEMailBody]") ??
			throw new InvalidOperationException("ConfirmEMailBody body not found");
		body = body.Replace("{0}", emailConfirmCode)
			.Replace("{1}", callbackUrl);

		await _mailService.SendAsync(user.UserName, subject, body);
	}

	[HttpPost]
	public async Task<IActionResult> Register([FromForm] RegisterViewModel model)
	{
		try
		{
			var isValid = await _antiforgery.IsRequestValidAsync(HttpContext);
			if (!isValid)
				return new JsonResult(JsonResponse.Error("AntiForgery"));

			if (model.Password == null || model.Login == null)
				return new JsonResult(JsonResponse.Error("Failed"));
			var user = new AppUser<Int64>()
			{
				UserName = model.Login,
				PersonName = model.PersonName,
				Email = model.Login,
				PhoneNumber = model.Phone,
                Locale = _currentUser.Locale.Locale,
            };

			if (IsMultiTenant)
				user.Tenant = -1; // tenant will be created

			var result = await _userManager.CreateAsync(user, model.Password);
			if (result.Succeeded)
			{
				RemoveAntiforgeryCookie();
				await SendConfirmCode(user);
				var tok = _protector.Protect(user.UserName);
				return LocalRedirect($"/account/confirmcode?token={tok}");
				/*
				var token = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultPhoneProvider);
				var user2 = await _userManager.FindByNameAsync(user.UserName)
					?? throw new InvalidOperationException("Internal error");
				var verified = await _userManager.VerifyTwoFactorTokenAsync(user2, TokenOptions.DefaultPhoneProvider, token);
				await _userStore.SetPhoneNumberConfirmedAsync(user, true, new CancellationToken());
				*/
			}
			else
			{
				// "DuplicateUserName", "DuplicateEmail", 
				if (result.Errors.Any(e => e.Code == "DuplicateUserName"))
					return new JsonResult(JsonResponse.Error("DuplicateUserName"));
				return new JsonResult(JsonResponse.Error(String.Join(", ", result.Errors.Select(x => x.Description))));
			}
		}
		catch (Exception tex)
		{
			return new JsonResult(JsonResponse.Error(tex));
		}
	}

	[HttpGet]
	public async Task<IActionResult> ForgotPassword()
	{
		RemoveNonAntiforgeryCookies();
		var m = new SimpleIdentityViewModel()
		{
			Title = await LoadTitleAsync(),
			Theme = _appTheme.MakeTheme(),
			RequestToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken
		};
		return View(m);
	}

    [HttpPost]
    public async Task<IActionResult> ForgotPassword([FromForm] ForgotPasswordViewModel model)
    {
        try
        {
            var isValid = await _antiforgery.IsRequestValidAsync(HttpContext);
            if (!isValid)
                return new JsonResult(JsonResponse.Error("AntiForgery"));

            if (model.Login == null)
                return new JsonResult(JsonResponse.Error("Failed"));
            var user = await _userManager.FindByNameAsync(model.Login);

            if (user == null || !user.EmailConfirmed || user.UserName == null)
                return new JsonResult(JsonResponse.Ok("Success"));

			if (!user.ChangePasswordEnabled)
                return new JsonResult(JsonResponse.Error("NotAllowed"));

            String code = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);

            String subject = _localizer.Localize(null, "@[ResetPassword]") ??
	            throw new InvalidOperationException("ConfirmEMail body not found");

			String body = _localizer.Localize(null, "@[ResetPasswordBody]") ??
                throw new InvalidOperationException("ResetPasswordBody body not found");
                body = body.Replace("{0}", code);

            await _mailService.SendAsync(user.UserName, subject, body);
			_logger.LogInformation("A letter has been sent to {user}", user.UserName);

			return new JsonResult(JsonResponse.Ok("Success"));
        }
        catch (Exception tex)
        {
            return new JsonResult(JsonResponse.Error(tex));
        }
    }

    [HttpPost]
    public async Task<IActionResult> CheckForgotPasswordCode(ForgotPasswordCodeViewModel model)
    {
        var isValid = await _antiforgery.IsRequestValidAsync(HttpContext);
        if (!isValid)
            return new JsonResult(JsonResponse.Error("AntiForgery"));
        if (model.Login == null)
            return new JsonResult(JsonResponse.Error("Failed"));
        var user = await _userManager.FindByNameAsync(model.Login);
        if (user == null)
            return new JsonResult(JsonResponse.Error("Failed"));
        var result = await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider, model.Code);
        if (result)
            return new JsonResult(JsonResponse.Ok());
        else
            return new JsonResult(JsonResponse.Error("InvalidCode"));
    }

	[HttpPost]
	public async Task<IActionResult> ResetPassword(ForgotPasswordChangeViewModel model)
	{
		try
		{
            var isValid = await _antiforgery.IsRequestValidAsync(HttpContext);
            if (!isValid)
                return new JsonResult(JsonResponse.Error("AntiForgery"));
            if (model.Login == null)
                return new JsonResult(JsonResponse.Error("Failed"));
            var user = await _userManager.FindByNameAsync(model.Login);
            if (user == null)
                return new JsonResult(JsonResponse.Error("Failed"));
            var result = await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider, model.Code);
            if (!result)
                return new JsonResult(JsonResponse.Error("Failed"));
			var changePasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);
			var identityResult = await _userManager.ResetPasswordAsync(user, changePasswordToken, model.Password);
			if (identityResult.Succeeded)
				return new JsonResult(JsonResponse.Redirect("/account/login"));
            return new JsonResult(JsonResponse.Error(String.Join(", ", identityResult.Errors)));
        }
        catch (Exception ex)
		{
            return new JsonResult(JsonResponse.Error(ex));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Invite(String token, String user)
    {
		var appuser = await _userManager.FindByNameAsync(user);
		if (appuser == null || appuser.IsEmpty)
			return NotFound();
		if (token == null)
            return NotFound();
        var verified = await _userManager.VerifyUserTokenAsync(appuser, _userManager.Options.Tokens.EmailConfirmationTokenProvider, UserManager<AppUser<Int64>>.ConfirmEmailTokenPurpose, token);
		if (!verified)
            return NotFound();
        var m = new InviteViewModel()
		{
			Login = user,
			Title = await LoadTitleAsync(),
			Theme = _appTheme.MakeTheme(),
			Token = token,
            RequestToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken
        };
        return View(m);
    }

    [HttpPost]
    public async Task<IActionResult> Invite([FromForm] InviteViewModel model)
    {
        try
        {
            var isValid = await _antiforgery.IsRequestValidAsync(HttpContext);
            if (!isValid)
                return new JsonResult(JsonResponse.Error("AntiForgery"));

            if (model.Password == null || model.Login == null || model.Token == null)
                return new JsonResult(JsonResponse.Error("Failed"));

            var user = await _userManager.FindByNameAsync(model.Login);
			if (user == null || user.IsEmpty)
                return new JsonResult(JsonResponse.Error("Failed"));

            var confirmResult = await _userManager.ConfirmEmailAsync(user, model.Token);
            if (!confirmResult.Succeeded)
                return new JsonResult(JsonResponse.Error(String.Join(", ", confirmResult.Errors.Select(x => x.Code))));

            var changePasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetPasswordResult = await _userManager.ResetPasswordAsync(user, changePasswordToken, model.Password);
            if (!resetPasswordResult.Succeeded)
                return new JsonResult(JsonResponse.Error(String.Join(", ", resetPasswordResult.Errors.Select(x => x.Code))));

            user.PersonName = model.PersonName;
            user.PhoneNumber = model.Phone;
            user.Flags = UpdateFlags.PersonName | UpdateFlags.PhoneNumber;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return new JsonResult(JsonResponse.Error(String.Join(", ", updateResult.Errors.Select(x => x.Code))));

            await _signInManager.SignInAsync(user, isPersistent: true);
            RemoveAntiforgeryCookie();

			var llprms = new ExpandoObject()
				{
					{ "Id", user.Id },
					{ "LastLoginHost", Request.Host.Host }
				};
			await _dbContext.ExecuteExpandoAsync(CatalogDataSource, $"[{SecuritySchema}].[UpdateUserLogin]", llprms);

			return LocalRedirect("/");
        }
        catch (Exception tex)
        {
            return new JsonResult(JsonResponse.Error(tex));
        }
    }

    private async Task DoLogout()
	{
		await _signInManager.SignOutAsync();
		HttpContext.Session.Clear();
		RemoveAllCookies();
	}

	[HttpGet]
	[HttpPost]
	public async Task<IActionResult> Logout()
	{
		await DoLogout();
		return LocalRedirect("/");
	}

	[HttpGet]
	[HttpPost]
	public Task<IActionResult> Logoff()
	{
		return Logout();
	}


	[HttpPost]
	public async Task<IActionResult> Logout2()
	{
		await DoLogout();
		var providers = _configuration.GetValue<String>("Identity:Providers")?.Split(',');
		if (providers != null && providers.Length == 1 && providers[0] != "Local")
			return Content("{\"showLogOut\":true}", MimeTypes.Application.Json);
		return Content("{}", MimeTypes.Application.Json);
	}

	[HttpGet]
	[AllowAnonymous]
	public async Task<IActionResult> LoggedOut()
	{
		var vm = new SimpleIdentityViewModel()
		{
			Title = await LoadTitleAsync(),
			Theme = _appTheme.MakeTheme()
		};
		return View(vm);
	}

	[Authorize]
    [AllowAnonymous]
    [HttpPost]
	public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordViewModel model)
	{
		try
		{
			if (User.Identity == null || User.Identity.Name == null)
				throw new InvalidOperationException("User not found");

			if (model.OldPassword == null || model.NewPassword == null)
				throw new InvalidOperationException("Invalid model");

			//if (User.Identity.IsUserOpenId())
			//throw new SecurityException("Invalid User type (openId?)");

			var user = await _userManager.FindByNameAsync(User.Identity.Name)
				?? throw new InvalidOperationException("User not found");

			if (!user.ChangePasswordEnabled)
				throw new InvalidOperationException("ChangePasswordNotAllowed");

			var ir = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
			if (ir.Succeeded)
				return new JsonResult(JsonResponse.Ok(String.Empty));
			else
				return new JsonResult(JsonResponse.Error(String.Join(", ", ir.Errors.Select(e => e.Code))));
		}
		catch (Exception ex)
		{
			return new JsonResult(JsonResponse.Error(ex));
		}
	}

	[HttpGet]
	public async Task<IActionResult> License()
	{
		var m = new SimpleIdentityViewModel()
		{
			Title = await LoadTitleAsync(),
			Theme = _appTheme.MakeTheme()
		};
		return View(m);
	}

	[HttpGet]
	public async Task<IActionResult> ConfirmCode([FromQuery] String? Token)
	{
		if (Token == null)
			return LocalRedirect("/account/login");
		var email = _protector.Unprotect(Token);
		var m = new ConfirmCodeViewModel()
		{
			Email = email,
			Token = Token,
			Title = await LoadTitleAsync(),
			Theme = _appTheme.MakeTheme(),
			RequestToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken
		};
		return View(m);
	}

	[HttpPost]
	public async Task<IActionResult> ConfirmCode([FromForm] ConfirmCodeViewModel model)
	{
		try
		{
			var isValid = await _antiforgery.IsRequestValidAsync(HttpContext);

			if (!isValid)
				return new JsonResult(JsonResponse.Error("AntiForgery"));
			if (String.IsNullOrEmpty(model.Token))
				return new JsonResult(JsonResponse.Error("Failed"));

			var userName = _protector.Unprotect(model.Token);
			if (userName != model.Email)
				return new JsonResult(JsonResponse.Error("Failed"));
			var user = await _userManager.FindByNameAsync(userName);

			if (user == null)
				return new JsonResult(JsonResponse.Error("Failed"));

			if (user.EmailConfirmed)
				return new JsonResult(JsonResponse.Error("AlreadyConfirmed"));

			var verified = await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider, model.Code.Trim());
			if (!verified)
				return new JsonResult(JsonResponse.Error("InvalidConfirmCode"));

			await _appTenantManager.RegisterUserComplete(user.Id);
			await _signInManager.SignInAsync(user, isPersistent: true);
			return LocalRedirect("/");
		}
		catch (Exception ex)
		{
			return new JsonResult(JsonResponse.Error(ex));
		}
	}

	[HttpGet]
	[ActionName("confirmemail")]
	public async Task<IActionResult> ConfirmEmail([FromQuery] String Token)
	{
		try
		{
			var tok = _protector.Unprotect(Token).Split('\t');
			var user = await _userManager.FindByIdAsync(tok[0]);
			if (user == null || user.IsEmpty)
				return NotFound();
			if (user.EmailConfirmed)
				return LocalRedirect("/");
			var verified = await _userManager.VerifyUserTokenAsync(user,
					_userManager.Options.Tokens.EmailConfirmationTokenProvider,
					UserManager<AppUser<Int64>>.ConfirmEmailTokenPurpose,
				tok[1]);
			if (verified)
			{
				await _appTenantManager.RegisterUserComplete(user.Id);
				await _signInManager.SignInAsync(user, isPersistent:true); 
				return LocalRedirect("/");
			}
			return NotFound();
		} 
		catch (Exception ex)
		{
			return NotFound(ex.Message);
		}
	}

	private async Task<IActionResult> Error(String message)
	{
		var model = new ErrorViewModel()
		{
			Title = await LoadTitleAsync(),
			Theme = _appTheme.MakeTheme(),
			RequestToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken,
			Message = message	
		};
		return View("Error", model);
	}

	[HttpGet]
	[ActionName("loginexternal")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginExternal([FromQuery] String? remoteError = null)
	{
		// https://github.com/dotnet/aspnetcore/blob/2728435cacd711dfebf0f7ea1a531752631329f2/src/Identity/UI/src/Areas/Identity/Pages/V5/Account/ExternalLogin.cshtml.cs
		/*
		 * Create user: UserName, Email, EmailConfirmed, IsExternalUser ??
		 */

		if (!String.IsNullOrEmpty(remoteError))
			return await Error(remoteError);

        var info = await _signInManager.GetExternalLoginInfoAsync();
		if (info == null)
			return await Error($"Invalid external login info");

		var email = info.Principal.Identity.GetClaimValue<String>(WellKnownClaims.OAuthEmail)
			?? throw new InvalidOperationException("Email not found");

		var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
		if (user == null || user.IsEmpty)
		{
			// not found by login, try to create external login
			user = await _userManager.FindByNameAsync(email);
			if (user == null || user.IsEmpty)
			{
				return await Error($"User '{email}' is not registered in the system. Contact your administrator.");
			}
			if (user.IsBlocked)
				return await Error($"User '{email}' is blocked. Contact your administrator.");
			await _userManager.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, null));
        }


        // check user name
        var name = info.Principal.Identity.GetClaimValue<String>(WellKnownClaims.Name);
        if (name != user.PersonName)
        {
            user.PersonName = name;
            user.Flags |= UpdateFlags.PersonName;
            await _userManager.UpdateAsync(user);
        }

		if (user.IsBlocked)
		{
			return await Error($"User '{email}' is blocked. Contact your administrator.");
		}

		var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

		if (!result.Succeeded)
			return await Error($"Invalid login for {email}");

		return Redirect("/");
	}

    private String GetInitPasswordToken(AppUser<Int64> user, String token)
    {
		var tokenData = new InitPasswordModel(
			UserId: user.Id,
			Token: token,
			Time: DateTime.UtcNow);
        var dataBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tokenData));
        var protectedData = _protector.Protect(dataBytes);
		return WebEncoders.Base64UrlEncode(protectedData);
    }


	InitPasswordModel GetModelFromToken(String token)
	{
		var byteData = WebEncoders.Base64UrlDecode(token);
		var data = _protector.Unprotect(byteData);
		var strJson = Encoding.UTF8.GetString(data);
		var result = JsonSerializer.Deserialize<InitPasswordModel>(strJson)
			?? throw new InvalidOperationException("Token is null");
		return result;
	}

	[HttpGet]
	public async Task<IActionResult> InitPassword([FromQuery] String? token)
	{
		_logger.LogInformation("InitPassword");

        if (token == null)
			return NotFound();
		try
		{
			var model = GetModelFromToken(token);

			if (model.Time - DateTime.UtcNow > TimeSpan.FromMinutes(5))
				return NotFound();

			var user = await _userManager.FindByIdAsync(model.UserId.ToString())
				?? throw new InvalidOperationException("User not found");

			if (!user.SetPassword)
				return NotFound();

			var vm = new InitPasswordViewModel()
			{
				Title = await LoadTitleAsync(),
				Theme = _appTheme.MakeTheme(),
				Login = user.UserName,
				Token = token,
				RequestToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken
			};
			return View(vm);
		}
		catch (Exception)
		{
			RemoveAllCookies();
			return NotFound();
		}
	}

	[HttpPost]
	public async Task<IActionResult> InitPassword([FromForm] InitPasswordViewModel model)
	{
		var isValid = await _antiforgery.IsRequestValidAsync(HttpContext);

		if (!isValid)
			return new JsonResult(JsonResponse.Error("AntiForgery"));

		try
		{
			var initPwd = GetModelFromToken(model.Token);

			var user = await _userManager.FindByIdAsync(initPwd.UserId.ToString())
				?? throw new InvalidOperationException("User not found");

			if (!user.SetPassword)
				throw new InvalidOperationException("Invalid user");

			var identityResult = await _userManager.ResetPasswordAsync(user, initPwd.Token, model.Password);

			if (identityResult.Succeeded)
			{
				var llprms = new ExpandoObject()
				{
					{ "Id", user.Id },
					{ "Set", false }
				};
				await _dbContext.ExecuteExpandoAsync(CatalogDataSource, $"[{SecuritySchema}].[User.SetSetPassword]", llprms);
				RemoveAntiforgeryCookie();
				return new JsonResult(JsonResponse.Redirect("/"));
			}
			else
				return new JsonResult(JsonResponse.Error(String.Join(", ", identityResult.Errors.Select(e => e.Code))));
		}
		catch (Exception ex) 
		{
			return new JsonResult(JsonResponse.Error(ex));
		}
	}

	[HttpGet]
	public async Task<IActionResult> TwoFactor([FromQuery] String login)
	{
		_logger.LogInformation("TwoFactor");

		if (String.IsNullOrEmpty(login))
			throw new InvalidOperationException("Login is null");

		var user = await _userManager.FindByNameAsync(login) ??
			throw new InvalidOperationException("User not found");

		var vm = new TwoFactorViewModel()
		{
			Title = await LoadTitleAsync(),
			Theme = _appTheme.MakeTheme(),
			Login = user.UserName ?? String.Empty,
			RequestToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken
		};
		return View(vm);
	}

	// https://github.com/chsakell/aspnet-core-identity/blob/master/AspNetCoreIdentity/Controllers/TwoFactorAuthenticationController.cs
	[HttpPost]
	public async Task<IActionResult> TwoFactor([FromForm] TwoFactorViewModel model)
	{
		var isValid = await _antiforgery.IsRequestValidAsync(HttpContext);

		if (!isValid)
			return new JsonResult(JsonResponse.Error("AntiForgery"));

		try
		{
			var user = await _userManager.FindByNameAsync(model.Login)
				?? throw new InvalidOperationException("User not found");

			// VERIFY TWO FACTOR CODE
			var twoFactorResult = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, model.Code);

			_logger.LogInformation("Two Factor Result: {twoFactorResult}", twoFactorResult.ToString());

			var signInResult = await _signInManager.TwoFactorAuthenticatorSignInAsync(model.Code, model.IsPersistent, true);
			if (signInResult.Succeeded)
			{
				RemoveAntiforgeryCookie();
				var returnUrl = model.ReturnUrl?.ToLowerInvariant();
				if (returnUrl == null || returnUrl.StartsWith("/account"))
					returnUrl = "/";
				return new JsonResult(JsonResponse.Redirect(returnUrl));
			}
			else
				return new JsonResult(JsonResponse.Error("InvalidCode"));
		}
		catch (Exception ex)
		{
			return new JsonResult(JsonResponse.Error(ex));
		}
	}
}


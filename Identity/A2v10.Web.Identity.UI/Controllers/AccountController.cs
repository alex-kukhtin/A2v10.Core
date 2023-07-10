// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Web.Identity.UI;

[Route("account/[action]")]
[ApiExplorerSettings(IgnoreApi = true)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[AllowAnonymous]
public class AccountController : Controller
{
	private readonly SignInManager<AppUser<Int64>> _signInManager;
	private readonly UserManager<AppUser<Int64>> _userManager;
	private readonly IAntiforgery _antiforgery;
	private readonly IDbContext _dbContext;
	private readonly IApplicationHost _host;
	private readonly IApplicationTheme _appTheme;
	private readonly IMailService _mailService;
	private readonly ILocalizer _localizer;
	private readonly IDataProtector _protector;
	private readonly IAppTenantManager _appTenantManager;

	public AccountController(SignInManager<AppUser<Int64>> signInManager, UserManager<AppUser<Int64>> userManager,
			IAntiforgery antiforgery, IApplicationHost host, IDbContext dbContext,
			IApplicationTheme appTheme, IMailService mailService, ILocalizer localizer,
			IDataProtectionProvider protectionProvider, IAppTenantManager appTenantManager)
	{
		_signInManager = signInManager;
		_userManager = userManager;
		_antiforgery = antiforgery;
		_dbContext = dbContext;
		_host = host;
		_appTheme = appTheme;
		_mailService = mailService;
		_localizer = localizer;
		_appTenantManager = appTenantManager;
		_protector = protectionProvider.CreateProtector("Login");
	}

	void RemoveAllCookies()
	{
		foreach (var key in Request.Cookies.Keys)
			Response.Cookies.Delete(key);
	}
	void RemoveAntiforgeryCookie()
	{
		foreach (var key in Request.Cookies.Keys.Where(x => x.Contains("Antiforgery")))
			Response.Cookies.Delete(key);
	}

	void RemoveNonAntiforgeryCookies()
	{
		foreach (var key in Request.Cookies.Keys.Where(x => !x.Contains("Antiforgery")))
			Response.Cookies.Delete(key);
	}

	[HttpGet]
	public async Task<IActionResult> Login(String returnUrl)
	{
		RemoveNonAntiforgeryCookies();

		var m = new LoginViewModel()
		{
			Title = await _dbContext.LoadAsync<AppTitleModel>(_host.CatalogDataSource, "a2sys.[AppTitle.Load]"),
			Theme = _appTheme.MakeTheme(),
			RequestToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken,
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

			if (user.SetPassword)
				return new JsonResult(JsonResponse.Ok("SetPassword"));

			var result = await _signInManager.PasswordSignInAsync(model.Login, model.Password, model.IsPersistent, lockoutOnFailure: true);
			if (result.Succeeded)
			{
				RemoveAntiforgeryCookie();
				var returnUrl = model.ReturnUrl?.ToLowerInvariant();
				if (returnUrl == null || returnUrl.StartsWith("/account"))
					returnUrl = "/";
				return LocalRedirect(returnUrl);
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
			Title = await _dbContext.LoadAsync<AppTitleModel>(_host.CatalogDataSource, "a2sys.[AppTitle.Load]"),
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
				PhoneNumber = model.Phone
			};

			if (_host.IsMultiTenant)
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
			Title = await _dbContext.LoadAsync<AppTitleModel>(_host.CatalogDataSource, "a2sys.[AppTitle.Load]"),
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
            return new JsonResult(JsonResponse.Ok("Success"));
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
		return Content("{}", MimeTypes.Application.Json);
	}

	[Authorize]
	[HttpPost]
	public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordViewModel model)
	{
		try
		{
			if (User.Identity == null || User.Identity.Name == null)
				throw new InvalidOperationException("User not found");

			if (model.OldPassword == null || model.NewPassword == null)
				throw new InvalidOperationException("Invalid model");

			//if (User.Identity.IsUserOpenId())
			//throw new SecurityException("Invalid User type (openId?)");

			var user = await _userManager.FindByIdAsync(User.Identity.Name)
				?? throw new InvalidOperationException("User not found");

			//if (!user.ChangePasswordEnabled)
			//throw new SecurityException("Change password not allowed");

			var ir = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
			if (ir.Succeeded)
			{
				await _userManager.UpdateAsync(user);
				return new JsonResult(JsonResponse.Ok(String.Empty));
			}
			else
			{
				return new JsonResult(JsonResponse.Error(String.Join(", ", ir.Errors)));
			}
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
			Title = await _dbContext.LoadAsync<AppTitleModel>(_host.CatalogDataSource, "a2sys.[AppTitle.Load]"),
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
			Title = await _dbContext.LoadAsync<AppTitleModel>(_host.CatalogDataSource, "a2sys.[AppTitle.Load]"),
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
}


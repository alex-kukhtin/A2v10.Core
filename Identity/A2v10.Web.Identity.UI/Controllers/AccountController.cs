// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
	private readonly AppUserStore<Int64> _userStore;
	private readonly IAntiforgery _antiforgery;
	private readonly IDbContext _dbContext;
	private readonly IApplicationHost _host;
	private readonly IApplicationTheme _appTheme;
	private readonly IMailService _mailService;

	public AccountController(SignInManager<AppUser<Int64>> signInManager, UserManager<AppUser<Int64>> userManager, 
			AppUserStore<Int64> userStore, 
			IAntiforgery antiforgery, IApplicationHost host, IDbContext dbContext, 
			IApplicationTheme appTheme, IMailService mailService)
	{
		_signInManager = signInManager;
		_userManager = userManager;
		_antiforgery = antiforgery;
		_userStore = userStore;
		_dbContext = dbContext;
		_host = host;
		_appTheme = appTheme;
		_mailService = mailService;
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


	[HttpGet]
	public async Task<IActionResult> Login(String returnUrl)
	{
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

	[HttpGet]
	public async Task<IActionResult> Register()
	{
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
		String emailConfirmLink = await _userManager.GenerateEmailConfirmationTokenAsync(user);
		String emailConfirmCode = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
		String subject = "subject";
		String body = "body";
		if (user.UserName == null)
			throw new InvalidOperationException();
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
				return LocalRedirect("/account/confirmcode");
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
		var m = new SimpleIdentityViewModel()
		{
			Title = await _dbContext.LoadAsync<AppTitleModel>(_host.CatalogDataSource, "a2sys.[AppTitle.Load]"),
			Theme = _appTheme.MakeTheme(),
			RequestToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken
		};
		return View(m);
	}

	[HttpGet]
	[HttpPost]
	public async Task<IActionResult> Logout()
	{
		await _signInManager.SignOutAsync();
		//HttpContext.Session.Clear(); ???
		RemoveAllCookies(); // TODO:
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
		await _signInManager.SignOutAsync();
		//HttpContext.Session.Clear(); ???
		RemoveAllCookies(); // TODO:
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
	public async Task<IActionResult> ConfirmCode()
	{
		var m = new SimpleIdentityViewModel()
		{
			Title = await _dbContext.LoadAsync<AppTitleModel>(_host.CatalogDataSource, "a2sys.[AppTitle.Load]"),
			Theme = _appTheme.MakeTheme()
		};
		return View(m);
	}
}


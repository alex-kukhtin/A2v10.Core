// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace A2v10.Web.Identity.UI
{
	[Route("account/[action]")]
	[ApiExplorerSettings(IgnoreApi = true)]
	public class AccountController : Controller
	{
		private readonly SignInManager<AppUser> _signInManager;

		public AccountController(SignInManager<AppUser> signInManager)
		{
			_signInManager = signInManager;
		}

		[AllowAnonymous]
		[HttpGet]
		public IActionResult Login(String returnUrl)
		{
			var m = new LoginViewModel();
			TempData["ReturnUrl"] = returnUrl;
			return View(m);
		}

		[AllowAnonymous]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Login(LoginViewModel model)
		{
			var result = await _signInManager.PasswordSignInAsync(model.Login, model.Password, model.IsPersistent, lockoutOnFailure: true);
			if (result.Succeeded)
			{
				var returnUrl = (TempData["ReturnUrl"] ?? "/").ToString().ToLowerInvariant();
				if (returnUrl.StartsWith("/account"))
					returnUrl = "/";
				return LocalRedirect(returnUrl);
			}
			throw new InvalidOperationException("Invalid login");
		}


		[HttpGet]
		[HttpPost]
		public async Task<IActionResult> Logout()
		{
			await _signInManager.SignOutAsync();
			//HttpContext.Session.Clear(); ???
			//ClearAllCookies(); // TODO:
			return LocalRedirect("/");
		}

		[HttpGet]
		[HttpPost]
		public Task<IActionResult> Logoff()
		{
			return Logout();
		}
	}
}

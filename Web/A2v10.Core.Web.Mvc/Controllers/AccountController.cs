using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

using A2v10.Core.Web.Mvc.ViewModels;
using A2v10.Web.Identity;

namespace A2v10.Core.Web.Mvc.Controllers
{
	[Route("account/[action]")]
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

		public async Task<IActionResult> Logoff()
		{
			await _signInManager.SignOutAsync();
			HttpContext.Session.Clear();
			//ClearAllCookies(); // TODO:
			return LocalRedirect("/");
		}
	}
}

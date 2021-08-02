// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
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
		private readonly IAntiforgery _antiforgery;
		private readonly IDbContext _dbContext;
		private readonly IApplicationHost _host;

		public AccountController(SignInManager<AppUser> signInManager, IAntiforgery antiforgery, IApplicationHost host, IDbContext dbContext)
		{
			_signInManager = signInManager;
			_antiforgery = antiforgery;
			_dbContext = dbContext;
			_host = host;

		}

		void RemoveAllCookies()
		{
			Response.Cookies.Delete(CookieNames.Application.Profile);
			Response.Cookies.Delete(CookieNames.Identity.State);
		}

		[AllowAnonymous]
		[HttpGet]
		public async Task<IActionResult> Login(String returnUrl)
		{
			RemoveAllCookies();
			var m = new LoginViewModel()
			{
				Title = await _dbContext.LoadAsync<AppTitleModel>(_host.CatalogDataSource, "a2ui.[AppTitle.Load]")
			};
			m.RequestToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken;
			TempData["ReturnUrl"] = returnUrl;
			return View(m);
		}

		[AllowAnonymous]
		[HttpPost]
		public async Task<IActionResult> Login([FromForm] LoginViewModel model)
		{
			var isValid = await _antiforgery.IsRequestValidAsync(HttpContext);
			//_antiforgery.ValidateRequestAsync
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
		[AllowAnonymous]
		public IActionResult Register()
		{
			return new EmptyResult();
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
	}
}

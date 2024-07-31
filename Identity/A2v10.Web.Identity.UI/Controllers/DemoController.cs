// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace A2v10.Web.Identity.UI;

[AllowAnonymous]
[Route("demo")]
public class DemoController(SignInManager<AppUser<Int64>> _signInManager, IConfiguration _configuration) : Controller
{
	[HttpGet]
	public async Task<IActionResult> Demo()
	{
		await _signInManager.SignOutAsync();
		HttpContext.Session.Clear();

		foreach (var key in Request.Cookies.Keys)
			Response.Cookies.Delete(key);

		var login = _configuration.GetValue<String>("DemoAccount:Login");
		var password = _configuration.GetValue<String>("DemoAccount:Password");
		if (String.IsNullOrEmpty(login) || String.IsNullOrEmpty(password))
			return LocalRedirect("/account/login");
		var signInResult = await _signInManager.PasswordSignInAsync(login, password, isPersistent:true, lockoutOnFailure:false);
		if (signInResult.Succeeded)
			return LocalRedirect("/");
		return LocalRedirect("/account/login");
	}
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using A2v10.Identity.UI;
using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace TestServices;

[TestClass]
[TestCategory("Registration")]
public class RegistrationAndCodesTests
{
	private static IActionResult? Filter(Boolean registration)
	{
		var services = new ServiceCollection();
		services.Configure<AppOptions>(o => o.Registration = registration);
		var http = new DefaultHttpContext() { RequestServices = services.BuildServiceProvider() };
		var context = new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()),
			[], new Dictionary<String, Object?>(), controller: new Object());
		new RegistrationAttribute().OnActionExecuting(context);
		return context.Result;
	}

	[TestMethod]
	public void Registration_is_closed_on_the_server_when_the_flag_is_off()
	{
		Assert.IsInstanceOfType<NotFoundResult>(Filter(registration: false));
		Assert.IsNull(Filter(registration: true));
	}

	// the user lives in memory; the lockout columns are its own properties, as AppUserStore keeps them
	private sealed class Store : IUserLockoutStore<AppUser<Int64>>
	{
		public Task<DateTimeOffset?> GetLockoutEndDateAsync(AppUser<Int64> user, CancellationToken ct) => Task.FromResult(user.LockoutEndDateUtc);
		public Task SetLockoutEndDateAsync(AppUser<Int64> user, DateTimeOffset? lockoutEnd, CancellationToken ct) { user.LockoutEndDateUtc = lockoutEnd; return Task.CompletedTask; }
		public Task<Int32> IncrementAccessFailedCountAsync(AppUser<Int64> user, CancellationToken ct) => Task.FromResult(++user.AccessFailedCount);
		public Task ResetAccessFailedCountAsync(AppUser<Int64> user, CancellationToken ct) { user.AccessFailedCount = 0; return Task.CompletedTask; }
		public Task<Int32> GetAccessFailedCountAsync(AppUser<Int64> user, CancellationToken ct) => Task.FromResult(user.AccessFailedCount);
		public Task<Boolean> GetLockoutEnabledAsync(AppUser<Int64> user, CancellationToken ct) => Task.FromResult(user.LockoutEnabled);
		public Task SetLockoutEnabledAsync(AppUser<Int64> user, Boolean enabled, CancellationToken ct) { user.LockoutEnabled = enabled; return Task.CompletedTask; }
		public Task<IdentityResult> UpdateAsync(AppUser<Int64> user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
		public Task<String> GetUserIdAsync(AppUser<Int64> user, CancellationToken ct) => Task.FromResult(user.Id.ToString());
		public Task<String?> GetUserNameAsync(AppUser<Int64> user, CancellationToken ct) => Task.FromResult(user.UserName);
		public Task SetUserNameAsync(AppUser<Int64> user, String? userName, CancellationToken ct) { user.UserName = userName; return Task.CompletedTask; }
		public Task<String?> GetNormalizedUserNameAsync(AppUser<Int64> user, CancellationToken ct) => Task.FromResult(user.UserName);
		public Task SetNormalizedUserNameAsync(AppUser<Int64> user, String? normalizedName, CancellationToken ct) => Task.CompletedTask;
		public Task<IdentityResult> CreateAsync(AppUser<Int64> user, CancellationToken ct) => throw new NotSupportedException();
		public Task<IdentityResult> DeleteAsync(AppUser<Int64> user, CancellationToken ct) => throw new NotSupportedException();
		public Task<AppUser<Int64>?> FindByIdAsync(String userId, CancellationToken ct) => throw new NotSupportedException();
		public Task<AppUser<Int64>?> FindByNameAsync(String normalizedUserName, CancellationToken ct) => throw new NotSupportedException();
		public void Dispose() { }
	}

	// the letter's code is "123456"; how it is made is the provider's business, not this check's
	private sealed class FixedCode : IUserTwoFactorTokenProvider<AppUser<Int64>>
	{
		public Task<String> GenerateAsync(String purpose, UserManager<AppUser<Int64>> manager, AppUser<Int64> user) => Task.FromResult("123456");
		public Task<Boolean> ValidateAsync(String purpose, String token, UserManager<AppUser<Int64>> manager, AppUser<Int64> user) => Task.FromResult(token == "123456");
		public Task<Boolean> CanGenerateTwoFactorTokenAsync(UserManager<AppUser<Int64>> manager, AppUser<Int64> user) => Task.FromResult(true);
	}

	private static UserManager<AppUser<Int64>> Manager()
	{
		var manager = new UserManager<AppUser<Int64>>(new Store(), Options.Create(new IdentityOptions()),
			null!, null!, null!, null!, null!, null!, NullLogger<UserManager<AppUser<Int64>>>.Instance);
		manager.RegisterTokenProvider(TokenOptions.DefaultEmailProvider, new FixedCode());
		return manager;
	}

	private static AppUser<Int64> NewUser() => new() { Id = 5, UserName = "user@example.com", LockoutEnabled = true };

	[TestMethod]
	public async Task The_sixth_wrong_code_finds_the_user_locked()
	{
		var manager = Manager();
		var user = NewUser();

		for (var i = 0; i < manager.Options.Lockout.MaxFailedAccessAttempts; i++)
			Assert.IsFalse(await EmailCodes.VerifyAsync(manager, user, "000000"));

		Assert.IsTrue(await manager.IsLockedOutAsync(user));
		Assert.IsFalse(await EmailCodes.VerifyAsync(manager, user, "123456"), "a locked user is refused even the right code");
	}

	[TestMethod]
	public async Task A_right_code_passes_and_clears_the_count()
	{
		var manager = Manager();
		var user = NewUser();

		Assert.IsFalse(await EmailCodes.VerifyAsync(manager, user, "000000"));
		Assert.AreEqual(1, user.AccessFailedCount);
		Assert.IsTrue(await EmailCodes.VerifyAsync(manager, user, "123456"));
		Assert.AreEqual(0, user.AccessFailedCount);
	}
}

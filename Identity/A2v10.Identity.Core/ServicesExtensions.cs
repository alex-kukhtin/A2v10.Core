// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

using A2v10.Web.Identity;

namespace Microsoft.Extensions.DependencyInjection
{
	public static class ServicesExtensions
	{
		public static IServiceCollection AddPlatformIdentityCore(this IServiceCollection services,
			Action<AppUserStoreOptions> options = null)
		{
			services.AddIdentityCore<AppUser>(options =>
			{
				options.User.RequireUniqueEmail = true;
				options.Lockout.MaxFailedAccessAttempts = 5;
				options.SignIn.RequireConfirmedEmail = true;
				options.Password.RequiredLength = 6;
			})
			.AddUserManager<UserManager<AppUser>>()
			.AddSignInManager<SignInManager<AppUser>>()
			.AddDefaultTokenProviders(); // for change password, email & phone validation

			services.AddScoped<AppUserStore>()
			.AddScoped<IUserStore<AppUser>>(s =>
			{
				return s.GetService<AppUserStore>();
			})
			.AddScoped<AppUserStoreOptions>(s =>
			{
				var opts = new AppUserStoreOptions();
				options?.Invoke(opts);
				return opts;
			})
			.AddScoped<ISecurityStampValidator, SecurityStampValidator<AppUser>>()
			.AddScoped<ISystemClock, SystemClock>();
			return services;
		}

		public static AuthenticationBuilder AddPlatformAuthentication(this IServiceCollection services)
		{
			var builder = services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
				options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
				options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
			})
			.AddCookie(IdentityConstants.ApplicationScheme, o =>
			{
				o.Cookie.Name = IdentityConstants.ApplicationScheme;
				o.LoginPath = new PathString("/account/login");
				o.ReturnUrlParameter = "returnurl";
				o.LogoutPath = "/account/logout";
				o.SlidingExpiration = true;
				o.ExpireTimeSpan = TimeSpan.FromDays(20);
				o.Events = new CookieAuthenticationEvents()
				{
					OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync
				};
			})
			.AddCookie(IdentityConstants.ExternalScheme, o =>
			{
				o.Cookie.Name = IdentityConstants.ExternalScheme;
				o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
			})
			.AddCookie(IdentityConstants.TwoFactorUserIdScheme,
				o =>
				{
					o.Cookie.Name = IdentityConstants.TwoFactorUserIdScheme;
					o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
				}
			);
			return builder;
		}
	}
}

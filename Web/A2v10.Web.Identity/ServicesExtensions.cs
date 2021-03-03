// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;

namespace A2v10.Web.Identity
{
	public static class ServicesExtensions
	{
		public static IServiceCollection AddPlatformIdentity(this IServiceCollection services)
		{
			services.AddIdentityCore<AppUser>(options =>
			{
				options.Lockout.MaxFailedAccessAttempts = 5;
				options.SignIn.RequireConfirmedEmail = true;
				options.SignIn.RequireConfirmedAccount = true;
				options.Password.RequiredLength = 6;

				options.User.RequireUniqueEmail = true;
			})
			.AddUserStore<AppUserStore>()
			.AddUserManager<UserManager<AppUser>>()
			.AddSignInManager<SignInManager<AppUser>>()
			.AddDefaultTokenProviders();

			services.AddScoped<IUserStore<AppUser>>(s => 
				new AppUserStore(
					s.GetService<IDbContext>()
				)
			)
			.AddScoped<ISecurityStampValidator, SecurityStampValidator<AppUser>>()
			.AddScoped<ISystemClock, SystemClock>();

			services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
				options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
				options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
			})
			.AddCookie(IdentityConstants.ApplicationScheme, o =>
			{
				o.LoginPath = new PathString("/account/login");
				o.ReturnUrlParameter = "returnurl";
				o.LogoutPath = "/account/logoff";
				o.Events = new CookieAuthenticationEvents()
				{
					OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync
				};
			})
			.AddCookie(IdentityConstants.ExternalScheme, o =>
			{
				o.Cookie.Name = IdentityConstants.ExternalScheme;
				o.ExpireTimeSpan = TimeSpan.FromDays(7);
				o.SlidingExpiration = true;
			})
			.AddCookie(IdentityConstants.TwoFactorRememberMeScheme,
					o => o.Cookie.Name = IdentityConstants.TwoFactorRememberMeScheme)
			.AddCookie(IdentityConstants.TwoFactorUserIdScheme,
				o =>
				{
					o.Cookie.Name = IdentityConstants.TwoFactorUserIdScheme;
					o.ExpireTimeSpan = TimeSpan.FromMinutes(5.0);
				}
			);

			return services;
		}
	}
}

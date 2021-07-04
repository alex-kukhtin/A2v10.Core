// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Web.Identity
{
	public static class ServicesExtensions
	{
		public static IMvcBuilder AddPlatformIdentity(this IMvcBuilder builder,
			Action<AppUserStoreOptions> options = null)
		{
			builder.Services.AddIdentityCore<AppUser>(options =>
			{
				options.User.RequireUniqueEmail = true;
				options.Lockout.MaxFailedAccessAttempts = 5;
				options.SignIn.RequireConfirmedEmail = true;
				options.Password.RequiredLength = 6;
			})
			.AddUserManager<UserManager<AppUser>>()
			.AddSignInManager<SignInManager<AppUser>>()
			.AddDefaultTokenProviders(); // for change password, email & phone validation

			builder.Services.AddScoped<IUserStore<AppUser>>(s =>
			{
				var host = s.GetService<IApplicationHost>();
				var opts = new AppUserStoreOptions()
				{
					Schema = "a2security",
				};

				if (host.IsMultiTenant)
					opts.DataSource = "Catalog";

				options?.Invoke(opts);

				return new AppUserStore(
					s.GetService<IDbContext>(), opts
				);
			}
			)
			.AddScoped<ISecurityStampValidator, SecurityStampValidator<AppUser>>()
			.AddScoped<ISystemClock, SystemClock>();

			builder.Services.AddAuthentication(options =>
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

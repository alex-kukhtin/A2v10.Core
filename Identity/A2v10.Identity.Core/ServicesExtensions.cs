// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

using A2v10.Web.Identity;
using Microsoft.Extensions.Configuration;

public static class ServicesExtensions
{
	public static IServiceCollection AddPlatformIdentityCore<T>(this IServiceCollection services,
		Action<IdentityOptions>? identityOptions = null) where T : struct
	{
		services.AddIdentityCore<AppUser<T>>(options =>
		{
			if (identityOptions != null)
				identityOptions(options);
			else
			{
				// default behaviour
				options.User.RequireUniqueEmail = true;
				options.Lockout.MaxFailedAccessAttempts = 5;
				options.SignIn.RequireConfirmedEmail = true;
				options.Password.RequiredLength = 6;
			}
		})
		.AddUserManager<UserManager<AppUser<T>>>()
		.AddSignInManager<SignInManager<AppUser<T>>>()
		.AddDefaultTokenProviders(); // for change password, email & phone validation

		services.AddScoped<AppUserStore<T>>()
		.AddScoped<IUserStore<AppUser<T>>>(s => s.GetRequiredService<AppUserStore<T>>())
		.AddScoped<IUserLoginStore<AppUser<T>>>(s => s.GetRequiredService<AppUserStore<T>>())
		.AddScoped<IUserClaimStore<AppUser<T>>>(s => s.GetRequiredService<AppUserStore<T>>())
		.AddScoped<ISecurityStampValidator, SecurityStampValidator<AppUser<T>>>()
		.AddScoped<ISystemClock, SystemClock>();
		return services;
	}

	public static IServiceCollection AddPlatformIdentityApi<T>(this IServiceCollection services) where T : struct
	{
		return services.AddScoped<AppUserStore<T>>()
		.AddScoped<IUserStore<AppUser<T>>>(s => s.GetRequiredService<AppUserStore<T>>())
		.AddScoped<IUserLoginStore<AppUser<T>>>(s => s.GetRequiredService<AppUserStore<T>>())
		.AddScoped<IUserClaimStore<AppUser<T>>>(s => s.GetRequiredService<AppUserStore<T>>());
	}

	public static AuthenticationBuilder AddPlatformAuthentication(this IServiceCollection services,
		String? cookiePrefix = null, Action<CookieAuthenticationEvents>? cockieEvents = null)
	{
		String px = !String.IsNullOrEmpty(cookiePrefix) ? $"{cookiePrefix}." : String.Empty;
		var builder = services.AddAuthentication(options =>
		{
			options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
			options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
			options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
		})
		.AddCookie(IdentityConstants.ApplicationScheme, o =>
		{
			o.Cookie.Name = px + IdentityConstants.ApplicationScheme;
			o.Cookie.SameSite = SameSiteMode.Strict;
			o.LoginPath = new PathString("/account/login");
			o.ReturnUrlParameter = "returnurl";
			o.LogoutPath = "/account/logout";
			o.SlidingExpiration = true;
			o.ExpireTimeSpan = TimeSpan.FromDays(20);
			o.Events = new CookieAuthenticationEvents()
			{
				OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync
			};
			cockieEvents?.Invoke(o.Events);
		})
		.AddCookie(IdentityConstants.ExternalScheme, o =>
		{
			o.Cookie.Name = px + IdentityConstants.ExternalScheme;
			o.Cookie.SameSite = SameSiteMode.Strict;
			o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
		})
		.AddCookie(IdentityConstants.TwoFactorUserIdScheme,
			o =>
			{
				o.Cookie.Name = px + IdentityConstants.TwoFactorUserIdScheme;
				o.Cookie.SameSite = SameSiteMode.Strict;
				o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
			}
		);
		return builder;
	}


	public static IServiceCollection AddIdentityConfiguration<T>(this IServiceCollection services, IConfiguration configuration)
		where T: struct
    {
		var sect = configuration.GetSection(AppUserStoreConfiguration.ConfigurationKey);
		if (sect == null)
			return services;
		var storeConfig = new AppUserStoreConfiguration();
		sect.Bind(storeConfig);

		services.AddOptions<AppUserStoreOptions<T>>();
		services.Configure<AppUserStoreOptions<T>>(opts =>
		{
			opts.Schema = storeConfig.Schema;
			opts.DataSource = storeConfig.DataSource;
			opts.MultiTenant = storeConfig.MultiTenant;
		});

		return services;
    }
}


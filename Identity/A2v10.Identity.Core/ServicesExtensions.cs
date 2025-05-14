// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

using A2v10.Web.Identity;
using A2v10.Identity.Core;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServicesExtensions
{
	public static void ConfigureDefaultIdentityOptions(IdentityOptions options)
	{
		var lockout = options.Lockout;
		lockout.MaxFailedAccessAttempts = 5;

		var pwd = options.Password;
		pwd.RequireDigit = false;
		pwd.RequiredLength = 6;
		pwd.RequireLowercase = false;
		pwd.RequireUppercase = false;
		pwd.RequireNonAlphanumeric = false;

		var si = options.SignIn;
		si.RequireConfirmedEmail = true;
		si.RequireConfirmedAccount = true;
		si.RequireConfirmedPhoneNumber = false;

		var us = options.User;
		us.RequireUniqueEmail = true;

		options.Tokens.AuthenticatorIssuer = "NovaEra";
	}
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
				ConfigureDefaultIdentityOptions(options);
			}
		})
		.AddUserManager<UserManager<AppUser<T>>>()
		.AddSignInManager<SignInManager<AppUser<T>>>()
		//.AddRoles<AppRole<T>>() /*TODO*/
		.AddDefaultTokenProviders(); // for change password, email & phone validation

		services.AddScoped<AppUserStore<T>>()
		.AddScoped<IUserStore<AppUser<T>>>(s => s.GetRequiredService<AppUserStore<T>>())
		.AddScoped<IUserLoginStore<AppUser<T>>>(s => s.GetRequiredService<AppUserStore<T>>())
		.AddScoped<IUserClaimStore<AppUser<T>>>(s => s.GetRequiredService<AppUserStore<T>>())
		.AddScoped<IUserTwoFactorStore<AppUser<T>>>(s => s.GetRequiredService<AppUserStore<T>>())
		//.AddScoped<IUserRoleStore<AppUser<T>>>(s => s.GetRequiredService<AppUserStore<T>>())
		.AddScoped<ISecurityStampValidator, SecurityStampValidator<AppUser<T>>>();
		      
		/* 
		services.AddScoped<AppRoleStore<T>>()
			.AddScoped<IRoleStore<AppRole<T>>>(s => s.GetRequiredService<AppRoleStore<T>>());
		*/
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

		SameSiteMode cookieMode = SameSiteMode.Lax;   /* Required for GOOGLE Auth*/

        var builder = services.AddAuthentication(options =>
		{
			options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
			options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
			options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
		})
		.AddCookie(IdentityConstants.ApplicationScheme, o =>
		{
			o.Cookie.Name = px + IdentityConstants.ApplicationScheme;
			o.Cookie.SameSite = cookieMode;
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
			o.Cookie.SameSite = cookieMode;
			o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
		})
		.AddCookie(IdentityConstants.TwoFactorUserIdScheme,
			o =>
			{
				o.Cookie.Name = px + IdentityConstants.TwoFactorUserIdScheme;
				o.Cookie.SameSite = cookieMode;
				o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
			}
		).
		AddCookie(IdentityConstants.TwoFactorRememberMeScheme, o =>
		{
            o.Cookie.Name = px + IdentityConstants.TwoFactorRememberMeScheme;
            o.Cookie.SameSite = cookieMode;
            o.ExpireTimeSpan = TimeSpan.FromDays(30);
        });

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

		if (!String.IsNullOrEmpty(storeConfig.AuthenticatorIssuer))
		{
			services.Configure<IdentityOptions>(opts =>
			{
				opts.Tokens.AuthenticatorIssuer = storeConfig.AuthenticatorIssuer;
			});
		}

		TimeSpan validationInterval = TimeSpan.FromSeconds(60 * 5);
		if (storeConfig.ValidationInterval != null)
			validationInterval = storeConfig.ValidationInterval.Value;


        services.Configure<SecurityStampValidatorOptions>(opts =>
        {
			opts.ValidationInterval = validationInterval; 
        });

        return services;
    }


	public static IServiceCollection AddDataProtectionSqlServer<T>(this IServiceCollection services, String appName)
        where T : struct
    {
        services.AddSingleton<IXmlRepository, SqlServerDataProtectionRepository<T>>();
        services.AddDataProtection().SetApplicationName(appName);
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
        {
            var repo = sp.GetRequiredService<IXmlRepository>();
            return new ConfigureOptions<KeyManagementOptions>(options =>
            {
                options.XmlRepository = repo;
            });
        });
        return services;
	}
}


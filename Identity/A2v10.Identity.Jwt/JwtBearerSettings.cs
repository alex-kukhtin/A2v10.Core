// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace A2v10.Identity.Jwt
{
	public record JwtBearerConfig
	{
		public String? Issuer { get; init; }
		public String? Audience { get; init; }
		public Int32 ExpireMinutes { get; init; }
		public String? SecurityKey { get; init; }
	}

	public class JwtBearerSettings
	{
		public String? Issuer { get; init; }
		public String? Audience { get; init; }
		public Int32 ExpireMinutes { get; init; }

		public SymmetricSecurityKey? SecurityKey { get; init; }

		private JwtBearerSettings()
		{
		}

		public static JwtBearerSettings Create(IConfiguration config)
		{
			var cfg = config.GetSection("Authentication:JwtBearer").Get<JwtBearerConfig>();

			if (cfg == null)
				throw new InvalidProgramException("Configuration key 'Authentication:JwtBearer' not found");

			if (String.IsNullOrEmpty(cfg.SecurityKey) || cfg.SecurityKey.Length < 16)
				throw new InvalidProgramException("Configuration key 'Authentication:JwtBearer.SecurityKey' must me at least 16 charactets long");

			Byte[] key = Encoding.UTF8.GetBytes(cfg.SecurityKey);

			return new JwtBearerSettings()
			{
				Audience = cfg.Audience,
				Issuer = cfg.Issuer,
				ExpireMinutes = cfg.ExpireMinutes,
				SecurityKey = new SymmetricSecurityKey(key)
			};
		}

		public void SetOptions(JwtBearerOptions options)
		{
			options.RequireHttpsMetadata = false;
			options.SaveToken = true;
			options.TokenValidationParameters = DefaultValidationParameters;
			options.Audience = Audience;
			options.ClaimsIssuer = Issuer;
			options.Events = new JwtBearerEvents()
			{
				OnAuthenticationFailed = (ctx) =>
				{
					if (ctx.Exception is SecurityTokenExpiredException ex)
					{
						ctx.Response.Headers.Add("Token-Expired", "true");
					}
					return Task.CompletedTask;
				}
			};
		}

		public TokenValidationParameters DefaultValidationParameters =>
			new ()
			{
				ValidateIssuerSigningKey = true,
				ValidateIssuer = false,
				ValidateAudience = false,
				ValidateActor = false,
				ValidateLifetime = true,
				IssuerSigningKey = SecurityKey,
				ClockSkew = TimeSpan.Zero,
			};
	}
}
// Copyright © 2021-2024 Oleksandr Kukhtin. All rights reserved.

using System;

using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.IdentityModel.Tokens;

using A2v10.Web.Identity;

namespace A2v10.Identity.Jwt;

public class JwtBearerService(JwtBearerSettings settings)
{
	private readonly JwtBearerSettings _settings = settings;

    public JwtTokenResult BuildToken<T>(AppUser<T> user) where T : struct
	{
		if (String.IsNullOrEmpty(user.UserName))
			throw new SecurityTokenException("UserName is null");

		var claims = new List<Claim>() {
			new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
			new(WellKnownClaims.NameIdentifier, user.Id.ToString()!),
			new(WellKnownClaims.Name, user.UserName)
		};

		if (user.Tenant != null)
			claims.Add(new Claim(WellKnownClaims.Tenant, user.Tenant.ToString()!));
		if (user.Organization != null)
			claims.Add(new Claim(WellKnownClaims.Organization, user.Organization.ToString()!));
		if (!String.IsNullOrEmpty(user.OrganizationKey))
			claims.Add(new Claim(WellKnownClaims.OrganizationKey, user.OrganizationKey));
		if (!String.IsNullOrEmpty(user.OrganizationTag))
			claims.Add(new Claim(WellKnownClaims.OrganizationTag, user.OrganizationTag));
		if (!String.IsNullOrEmpty(user.Segment))
			claims.Add(new Claim(WellKnownClaims.Segment, user.Segment));
		if (!String.IsNullOrEmpty(user.Locale))
			claims.Add(new Claim(WellKnownClaims.Locale, user.Locale));

		var expires = DateTime.UtcNow.AddMinutes(_settings.ExpireMinutes);

		var token = new JwtSecurityToken(
			issuer: _settings.Issuer,
			audience: _settings.Audience,
			claims: claims,
			expires: expires,
			signingCredentials: new SigningCredentials(
				_settings.SecurityKey,
				SecurityAlgorithms.HmacSha256
			)
		);

		var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

		return new JwtTokenResult(
			Expires :expires,
			Response : new JwtTokenResponse(
				accessToken: accessToken,
				refreshToken: GenerateRefreshToken(),
				user: user.UserName,
				personName: user.PersonName ?? user.UserName,
				validTo: token.ValidTo.ToUnixTime()
			)
		);
	}

	public static String GenerateRefreshToken()
	{
		using var rndGen = RandomNumberGenerator.Create();
		var randomBytes = new Byte[64];
		rndGen.GetBytes(randomBytes);
		return Convert.ToBase64String(randomBytes);
	}

	public AppUser<T> ExtractPrincipalFromToken<T>(String token) where T : struct
	{
		var handler = new JwtSecurityTokenHandler();

		var prms = _settings.DefaultValidationParameters;
		prms.ValidateLifetime = false; // token has already expired

		var principal = handler.ValidateToken(token, prms, out SecurityToken validatedToken);

		if (validatedToken.Issuer != _settings.Issuer)
			throw new SecurityTokenInvalidIssuerException();

		if (principal != null && principal.Identity != null)
		{
			return new AppUser<T>()
			{
				Id = principal.Identity.GetUserId<T>(),
				UserName = principal.Identity.GetUserClaim(WellKnownClaims.Name),
				Tenant = principal.Identity.GetUserTenant<T>(),
				Organization = principal.Identity.GetUserOrganization<T>(),
				OrganizationKey = principal.Identity.GetUserOrganizationKey(),
				OrganizationTag = principal.Identity.GetUserOrganizationTag(),
				Locale = principal.Identity.GetUserLocale()
			};
		}
		throw new SecurityTokenValidationException();
	}
}


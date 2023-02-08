// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.IdentityModel.Tokens;

using A2v10.Web.Identity;

namespace A2v10.Identity.Jwt;

public class JwtBearerService
{
	private readonly JwtBearerSettings _settings;

	public JwtBearerService(JwtBearerSettings settings)
	{
		_settings = settings;
	}

	public JwtTokenResult BuildToken<T>(AppUser<T> user) where T : struct
	{
		if (String.IsNullOrEmpty(user.UserName))
			throw new SecurityTokenException("UserName is null");

		var claims = new List<Claim>() {
			new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
			new Claim(WellKnownClaims.NameIdentifier, user.Id.ToString()!),
			new Claim(WellKnownClaims.Name, user.UserName)
		};
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
				UserName = principal.Identity.GetUserClaim(WellKnownClaims.Name)
			};
		}
		throw new SecurityTokenValidationException();
	}
}


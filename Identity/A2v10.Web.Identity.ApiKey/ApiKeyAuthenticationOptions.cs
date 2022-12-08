// Copyright © 2020-2022 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;

namespace A2v10.Web.Identity.ApiKey;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
	public const String DefaultScheme = "API Key";
	public const String Scheme = DefaultScheme;
	public const String AuthenticationType = DefaultScheme;
	public const String HeaderName = "X-Api-Key";

	public static OpenApiSecurityScheme OpenApiSecurityScheme =>
		new ()
		{
			Type = SecuritySchemeType.ApiKey,
			In = ParameterLocation.Header,
			Name = ApiKeyAuthenticationOptions.HeaderName,
			Scheme = ApiKeyAuthenticationOptions.Scheme
		};
	public static OpenApiSecurityRequirement OpenApiSecurityRequirement
	{
		get
		{
			var key = new OpenApiSecurityScheme()
			{
				Reference = new OpenApiReference()
				{
					Type = ReferenceType.SecurityScheme,
					Id = ApiKeyAuthenticationOptions.Scheme
				}
			};
			var rq = new OpenApiSecurityRequirement() {
				{ key, new List<String>() }
			};
			return rq;
		}
	}
}

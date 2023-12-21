// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

namespace A2v10.Identity.Jwt;

public class JwtBearerAuthenticationOptions : AuthenticationSchemeOptions
{
	public const String DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
	public const String Scheme = DefaultScheme;
	public const String AuthenticationType = DefaultScheme;

	public static OpenApiSecurityScheme OpenApiSecurityScheme =>
		new ()
		{
			Type = SecuritySchemeType.Http,
			In = ParameterLocation.Header,
			Scheme = JwtBearerDefaults.AuthenticationScheme
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
					Id = Scheme
				}
			};
			var rq = new OpenApiSecurityRequirement() {
				{ key, new List<String>() }
			};
			return rq;
		}
	}
}

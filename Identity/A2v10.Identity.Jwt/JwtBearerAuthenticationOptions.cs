// Copyright © 2020-2025 Oleksandr Kukhtin. All rights reserved.

using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace A2v10.Identity.Jwt;

public class JwtBearerAuthenticationOptions : AuthenticationSchemeOptions
{
	public const String DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
	public const String Scheme = DefaultScheme;
	public const String AuthenticationType = DefaultScheme;

}

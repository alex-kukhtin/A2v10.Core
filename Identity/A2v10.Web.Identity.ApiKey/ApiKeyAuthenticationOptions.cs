// Copyright © 2020-2025 Oleksandr Kukhtin. All rights reserved.

using System;
using Microsoft.AspNetCore.Authentication;

namespace A2v10.Web.Identity.ApiKey;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
	public const String DefaultScheme = ApiKeyDefaults.AuthenticationScheme;
	public const String Scheme = DefaultScheme;
	public const String AuthenticationType = DefaultScheme;
	public const String HeaderName = "X-Api-Key";
}

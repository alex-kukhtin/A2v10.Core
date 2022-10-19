// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Authentication;

namespace A2v10.Web.Identity.ApiKey;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
	public const String DefaultScheme = "API Key";
	public const String Scheme = DefaultScheme;
	public const String AuthenticationType = DefaultScheme;
}

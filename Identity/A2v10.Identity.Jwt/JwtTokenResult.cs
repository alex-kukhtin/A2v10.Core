// Copyright © 2021-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace A2v10.Identity.Jwt;

#pragma warning disable IDE1006 // Naming Styles
public record JwtTokenResponse(String accessToken, String refreshToken, Int64 validTo, String user, String personName, Boolean success = true );
public record JwtTokenError(String message)
{
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
    public Boolean success => false;
}
#pragma warning restore IDE1006 // Naming Styles


public record JwtTokenResult(DateTime Expires, JwtTokenResponse Response);
// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Identity.Jwt;

#pragma warning disable IDE1006 // Naming Styles
public record JwtTokenResponse(String accessToken, String refreshToken, Int64 validTo, String user, Boolean success = true );
public record JwtTokenError(String message)
{
#pragma warning disable CA1822 // Mark members as static
    public Boolean success => false;
#pragma warning restore CA1822 // Mark members as static
}
#pragma warning restore IDE1006 // Naming Styles


public record JwtTokenResult(DateTime Expires, JwtTokenResponse Response);
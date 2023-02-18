// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Security.Cryptography;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Http;

using A2v10.Data.Interfaces;
using A2v10.Web.Identity;

namespace A2v10.Platform.Web
{
    public class WebTokenProvider : ITokenProvider
    {
        private readonly IHttpContextAccessor _httpContext;

        public WebTokenProvider(IHttpContextAccessor httpContext)
        {
            _httpContext = httpContext;
        }

        public String GenerateToken(Guid accessToken)
        {
            if (accessToken == Guid.Empty)
                throw new InvalidProgramException("AccessToken for GenerateToken is empty");
            var ctx = _httpContext.HttpContext;
            if (ctx == null)
                throw new InvalidProgramException("Context is null");
            var sessionId = ctx.Session.Id;
            // TODO:??? Session Id???
            ctx.Session.SetString("SessionID", sessionId);
            var userId = ctx?.User?.Identity.GetUserId<Int64>();
            String key = $":{sessionId}:{accessToken}:{userId}:";
            using var algo = SHA256.Create();
            var hash = algo.ComputeHash(Encoding.UTF8.GetBytes(key));
            return WebEncoders.Base64UrlEncode(hash);
        }
    }
}

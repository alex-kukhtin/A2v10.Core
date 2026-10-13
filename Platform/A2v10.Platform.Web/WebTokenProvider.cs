// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Text;
using System.Security.Cryptography;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Http;

using A2v10.Data.Interfaces;
using A2v10.Web.Identity;

namespace A2v10.Platform.Web
{
	public class WebTokenProvider(IHttpContextAccessor httpContext) : ITokenProvider
	{
		private readonly IHttpContextAccessor _httpContext = httpContext;

        public String GenerateToken(Guid accessToken)
		{
			if (accessToken == Guid.Empty)
				throw new InvalidProgramException("AccessToken for GenerateToken is empty");
			var ctx = _httpContext.HttpContext
				?? throw new InvalidProgramException("Context is null");
			// No session in the key: Session.Id dies with 20 idle minutes or a restart, and a token
			// issued by Load had to survive an open form until Save. The user keeps it unforwardable.
			// Price: a blob overwritten under the same AccessToken (the old Image) may be served from
			// the browser cache until it expires
			var userId = ctx.User?.Identity.GetUserId<Int64>();
			String key = $":{accessToken}:{userId}:";
			var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
			return WebEncoders.Base64UrlEncode(hash);
		}
	}
}

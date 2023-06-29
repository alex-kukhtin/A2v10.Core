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
			var ctx = _httpContext.HttpContext 
				?? throw new InvalidProgramException("Context is null");
            var sessionId = ctx.Session.Id;
			// TODO:??? Session Id???
			ctx.Session.SetString("SessionID", sessionId);
			var userId = ctx?.User?.Identity.GetUserId<Int64>();
			String key = $":{sessionId}:{accessToken}:{userId}:";
			var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
			return WebEncoders.Base64UrlEncode(hash);
		}
	}
}

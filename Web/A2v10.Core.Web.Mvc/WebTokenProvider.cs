using System;
using System.Text;
using System.Security.Cryptography;

using A2v10.Data.Interfaces;
using A2v10.Web.Identity;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Http;

namespace A2v10.Core.Web.Mvc
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
				return null;
			var sessionId = ctx.Session.Id;
			var userId = ctx.User.Identity.GetUserId<Int64>();
			String key = $":{sessionId}:{accessToken}:{userId}:";
			using var algo = SHA256.Create();
			var hash = algo.ComputeHash(Encoding.UTF8.GetBytes(key));
			var xxxx = WebEncoders.Base64UrlEncode(hash);
			return WebEncoders.Base64UrlEncode(hash);
			//return HttpServerUtility.UrlTokenEncode(hash);
		}
	}
}

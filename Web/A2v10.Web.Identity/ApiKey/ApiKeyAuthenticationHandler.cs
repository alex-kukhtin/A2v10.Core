
using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// based on: https://josef.codes/asp-net-core-protect-your-api-with-api-keys/

namespace A2v10.Web.Identity.ApiKey
{
	public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
	{
		public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
			: base(options, logger, encoder, clock)
		{
		}

		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			throw new NotImplementedException();
		}

		protected override Task HandleChallengeAsync(AuthenticationProperties properties)
		{
			return base.HandleChallengeAsync(properties);
		}

		protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
		{
			return base.HandleForbiddenAsync(properties);
		}
	}
}

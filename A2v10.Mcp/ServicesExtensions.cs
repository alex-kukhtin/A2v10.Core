// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

using ModelContextProtocol.AspNetCore.Authentication;

using A2v10.Infrastructure;
using A2v10.Web.Identity;
using A2v10.Mcp;
using A2v10.Mcp.OAuth;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServicesExtensions
{
	const String McpScheme = OAuthController.AccessTokenScheme;
	const String McpPolicy = "A2v10.Mcp";

	public static IServiceCollection UseMcp(this IServiceCollection services, IConfiguration configuration,
		Action<McpBuilder>? mcp = null)
	{
		var publicUrl = configuration.GetValue<String>(McpAddress.ConfigurationKey)?.TrimEnd('/')
			?? throw new InvalidOperationException($"Configuration key '{McpAddress.ConfigurationKey}' not found");
		var address = new McpAddress(publicUrl);
		services.AddSingleton(address);

		// The ticket handler would answer a bare 401; the MCP scheme adds resource_metadata to it.
		services.AddAuthentication()
			.AddBearerToken(McpScheme, o => o.ForwardChallenge = McpAuthenticationDefaults.AuthenticationScheme)
			.AddMcp(o =>
			{
				o.ResourceMetadata = new()
				{
					Resource = address.Resource,
					AuthorizationServers = { address.Issuer },
					ScopesSupported = [OAuthController.Scope, OAuthController.OfflineAccess],
				};
			});

		services.AddAuthorizationBuilder()
			.AddPolicy(McpPolicy, p => p.AddAuthenticationSchemes(McpScheme).RequireAuthenticatedUser());

		// Stateless: a tool runs in the request's scope, where CurrentUserMiddleware has set up ICurrentUser.
		services.AddMcpServer()
			.WithHttpTransport(o =>
			{
				o.Stateless = true;
				// invoked per request in stateless; the text is the application's, this only hands it over
				o.ConfigureSessionOptions = async (context, options, _) =>
				{
					if (context.RequestServices.GetService<IPlatformMcpInstructions>() is { } instructions)
						options.ServerInstructions = await instructions.GetInstructionsAsync();
				};
			})
			.WithListToolsHandler(McpToolHandlers.ListTools)
			.WithCallToolHandler(McpToolHandlers.CallTool);
		mcp?.Invoke(new McpBuilder(services));

		services.AddSingleton<OAuthProtector>();
		services.AddScoped<IOAuthConsent, OAuthConsent>();
		services.AddControllers()
			.AddApplicationPart(typeof(OAuthController).Assembly);

		services.AddTransient<IStartupFilter, McpEndpointFilter>();
		return services;
	}

	/* The host's Configure closes its UseEndpoints inside ConfigurePlatform, and /mcp is the SDK's
	 * transport, not a controller. A second UseEndpoints after the host's pipeline joins the same
	 * route table: the matcher is built from all data sources on the first request.
	 */
	sealed class McpEndpointFilter : IStartupFilter
	{
		public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
		{
			next(app);
			app.UseEndpoints(e =>
			{
				e.MapMcp(McpAddress.McpPath).RequireAuthorization(McpPolicy);
				// Stateless has no GET stream; without this the platform's catch-all answers it with a login page.
				e.MapGet(McpAddress.McpPath, () => Results.StatusCode(StatusCodes.Status405MethodNotAllowed));
			});
		};
	}
}

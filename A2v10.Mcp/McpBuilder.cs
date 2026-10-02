// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Mcp;

/* The registry is IEnumerable<IPlatformMcpToolProvider> of the request scope; there is no registry
 * class. Two methods, not one: C# does not overload by constraint. Adding a tool twice is not
 * deduplicated - two providers with one name fail tools/list loudly.
 */
public sealed class McpToolsBuilder
{
	private readonly IServiceCollection _services;

	internal McpToolsBuilder(IServiceCollection services)
	{
		_services = services;
	}

	public McpToolsBuilder Add<T>() where T : class, IPlatformMcpTool
	{
		_services.AddScoped<T>();
		_services.AddScoped<IPlatformMcpToolProvider, SingleToolProvider<T>>();
		return this;
	}

	public McpToolsBuilder AddProvider<P>() where P : class, IPlatformMcpToolProvider
	{
		_services.AddScoped<IPlatformMcpToolProvider, P>();
		return this;
	}
}

internal sealed class SingleToolProvider<T>(T tool) : IPlatformMcpToolProvider where T : IPlatformMcpTool
{
	public IEnumerable<IPlatformMcpTool> GetTools() => [tool];
}

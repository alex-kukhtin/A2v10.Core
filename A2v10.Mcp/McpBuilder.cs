// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Mcp;

/* What the host gives MCP: tools and the instructions.
 * The registry is IEnumerable<IPlatformMcpToolProvider> of the request scope; there is no registry
 * class. Two methods, not one: C# does not overload by constraint. Adding a tool twice is not
 * deduplicated - two providers with one name fail tools/list loudly.
 */
public sealed class McpBuilder
{
	private readonly IServiceCollection _services;

	internal McpBuilder(IServiceCollection services)
	{
		_services = services;
	}

	public McpBuilder Add<T>() where T : class, IPlatformMcpTool
	{
		_services.AddScoped<T>();
		_services.AddScoped<IPlatformMcpToolProvider, SingleToolProvider<T>>();
		return this;
	}

	public McpBuilder AddProvider<P>() where P : class, IPlatformMcpToolProvider
	{
		_services.AddScoped<IPlatformMcpToolProvider, P>();
		return this;
	}

	// one text per application: a second registration, here or past the builder, fails at start
	public McpBuilder Instructions<I>() where I : class, IPlatformMcpInstructions
	{
		if (_services.FirstOrDefault(d => d.ServiceType == typeof(IPlatformMcpInstructions)) is { } first)
			throw new InvalidOperationException(
				$"MCP instructions are one per application: {first.ImplementationType?.FullName} is registered, {typeof(I).FullName} is not added");
		_services.AddScoped<IPlatformMcpInstructions, I>();
		return this;
	}
}

internal sealed class SingleToolProvider<T>(T tool) : IPlatformMcpToolProvider where T : IPlatformMcpTool
{
	public Task<IEnumerable<IPlatformMcpTool>> GetToolsAsync() => Task.FromResult<IEnumerable<IPlatformMcpTool>>([tool]);
}

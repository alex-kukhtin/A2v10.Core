// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using A2v10.Infrastructure;

namespace A2v10.Mcp;

/* tools/list and tools/call around IPlatformMcpTool. Stateless: RequestContext.Services is the
 * request's scope, so both enumerate the providers anew and see the request's ICurrentUser.
 */
internal static class McpToolHandlers
{
	static readonly JsonSerializerOptions _resultOptions = new(JsonSerializerDefaults.Web);

	public static ValueTask<ListToolsResult> ListTools(RequestContext<ListToolsRequestParams> request, CancellationToken _)
	{
		var tools = VisibleTools(request.Services!).Select(t => new Tool()
		{
			Name = t.Name,
			Description = t.Description,
			InputSchema = t.InputSchema,
		});
		return ValueTask.FromResult(new ListToolsResult() { Tools = [.. tools] });
	}

	public static async ValueTask<CallToolResult> CallTool(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
	{
		var name = request.Params?.Name;
		// Hidden by roles answers exactly as absent: the difference would reveal the tool exists.
		var tool = VisibleTools(request.Services!).FirstOrDefault(t => t.Name == name)
			?? throw new McpProtocolException($"Unknown tool: '{name}'", McpErrorCode.InvalidParams);
		var args = JsonSerializer.SerializeToElement(request.Params?.Arguments ?? new Dictionary<String, JsonElement>());
		try
		{
			var result = await tool.ExecuteAsync(args, cancellationToken);
			return new CallToolResult() { Content = [new TextContentBlock() { Text = JsonSerializer.Serialize(result, _resultOptions) }] };
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Any exception, not only a refusal: the model acts as the same user. The stack stays here.
			return new CallToolResult() { IsError = true, Content = [new TextContentBlock() { Text = ex.Message }] };
		}
	}

	/* Names are checked on the full set, before the roles filter: otherwise a collision with a hidden
	 * tool fails for some users and not for others. A provider returns anything, so this is the
	 * earliest place to catch it.
	 */
	static IEnumerable<IPlatformMcpTool> VisibleTools(IServiceProvider services)
	{
		var all = services.GetServices<IPlatformMcpToolProvider>()
			.SelectMany(p => p.GetTools().Select(t => (Provider: p, Tool: t)))
			.ToList();
		foreach (var g in all.GroupBy(x => x.Tool.Name).Where(g => g.Count() > 1))
			throw new InvalidOperationException($"MCP tool '{g.Key}' is declared more than once: {String.Join(", ", g.Select(x => Source(x.Provider, x.Tool)))}");
		var roles = services.GetRequiredService<ICurrentUser>().Identity.Roles;
		return all.Select(x => x.Tool).Where(t => IsAllowed(t.Roles, roles));
	}

	// ModelJson.CheckRoles: null is everyone; otherwise the user's roles must intersect. No implicit Admin.
	static Boolean IsAllowed(String[]? toolRoles, IEnumerable<String>? userRoles)
	{
		if (toolRoles == null)
			return true;
		if (userRoles == null)
			return false;
		return toolRoles.Intersect(userRoles).Any();
	}

	static String Source(IPlatformMcpToolProvider provider, IPlatformMcpTool tool)
	{
		var pt = provider.GetType();
		if (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(SingleToolProvider<>))
			return tool.GetType().FullName!;
		return $"{tool.GetType().FullName} from {pt.FullName}";
	}
}

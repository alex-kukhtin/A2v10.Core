// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
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
	/* Relaxed: the default encoder writes every non-ASCII letter as \uXXXX - twice the text for Ukrainian
	 * data, more in tokens, and the model decodes each name. A Create(ranges) encoder still escapes the
	 * apostrophe of "Об'єкт" and needs the list of languages the data may hold. 'Unsafe' is about
	 * embedding in HTML; this text goes to the model inside JSON-RPC.
	 */
	static readonly JsonSerializerOptions _resultOptions = new(JsonSerializerDefaults.Web)
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public static async ValueTask<ListToolsResult> ListTools(RequestContext<ListToolsRequestParams> request, CancellationToken _)
	{
		var tools = (await VisibleToolsAsync(request.Services!)).Select(t => new Tool()
		{
			Name = t.Name,
			Title = t.Title,
			Description = t.Description,
			InputSchema = t.InputSchema,
			// all four, always: an absent hint means the spec's default, and two of those are 'true'
			Annotations = new ToolAnnotations()
			{
				ReadOnlyHint = t.Hints.HasFlag(PlatformMcpToolHints.ReadOnly),
				DestructiveHint = t.Hints.HasFlag(PlatformMcpToolHints.Destructive),
				IdempotentHint = t.Hints.HasFlag(PlatformMcpToolHints.Idempotent),
				OpenWorldHint = t.Hints.HasFlag(PlatformMcpToolHints.OpenWorld),
			}
		});
		return new ListToolsResult() { Tools = [.. tools] };
	}

	public static async ValueTask<CallToolResult> CallTool(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
	{
		var name = request.Params?.Name;
		// Hidden by roles answers exactly as absent: the difference would reveal the tool exists.
		var tool = (await VisibleToolsAsync(request.Services!)).FirstOrDefault(t => t.Name == name)
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
	static async Task<IEnumerable<IPlatformMcpTool>> VisibleToolsAsync(IServiceProvider services)
	{
		var all = new List<(IPlatformMcpToolProvider Provider, IPlatformMcpTool Tool)>();
		foreach (var p in services.GetServices<IPlatformMcpToolProvider>())
			all.AddRange((await p.GetToolsAsync()).Select(t => (p, t)));
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

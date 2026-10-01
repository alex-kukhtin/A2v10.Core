// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

/* The contract of interaction with an MCP tool: what tools/list shows and how tools/call runs it.
 * BCL types only - A2v10.Metadata implements it without pulling the MCP SDK.
 * Every request creates every tool, so a constructor takes dependencies and does no work.
 */
public interface IPlatformMcpTool
{
	String Name { get; }
	String Description { get; }
	JsonElement InputSchema { get; }
	// null is everyone; otherwise the user's roles must intersect it. Admin does not pass implicitly.
	String[]? Roles { get; }
	// The result is serialized to JSON for the model; an exception goes back as isError with its text.
	Task<Object?> ExecuteAsync(JsonElement args, CancellationToken cancellationToken);
}

// Scoped, resolved per request: the set of tools may depend on the user.
public interface IPlatformMcpToolProvider
{
	IEnumerable<IPlatformMcpTool> GetTools();
}

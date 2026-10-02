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
/* What the tool does to its world, as MCP's ToolAnnotations say it. Every hint is written to the client
 * explicitly, an absent flag as false - the spec's own defaults are destructive and open-world.
 * OpenWorld is a tool that reaches outside the application (a bank's API), never its database.
 */
[Flags]
public enum PlatformMcpToolHints
{
	None = 0,
	ReadOnly = 1,
	Destructive = 2,
	Idempotent = 4,
	OpenWorld = 8
}

public interface IPlatformMcpTool
{
	String Name { get; }
	// for a human: the client's list of tools and its permissions screen; the model reads Description
	String Title { get; }
	String Description { get; }
	PlatformMcpToolHints Hints { get; }
	JsonElement InputSchema { get; }
	// null is everyone; otherwise the user's roles must intersect it. Admin does not pass implicitly.
	String[]? Roles { get; }
	// The result is serialized to JSON for the model; an exception goes back as isError with its text.
	Task<Object?> ExecuteAsync(JsonElement args, CancellationToken cancellationToken);
}

/* Scoped, resolved per request: the set of tools may depend on the user.
 * Async: a provider's first call may load what its schemas are built from (the metadata one reads mcp.json).
 */
public interface IPlatformMcpToolProvider
{
	Task<IEnumerable<IPlatformMcpTool>> GetToolsAsync();
}

/* The application's MCP instructions: one text per application, never assembled from parts.
 * Scoped, asked on every request to /mcp (stateless); null is no instructions.
 */
public interface IPlatformMcpInstructions
{
	Task<String?> GetInstructionsAsync();
}

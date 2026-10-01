// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Mcp;

/* The URL the user typed into the client, not the one Kestrel listens on: behind a proxy they
 * differ, and Claude requires `resource` to equal what was typed and the token to be bound to it.
 * Hence configuration, never the request.
 */
public record McpAddress(String Issuer)
{
	public const String ConfigurationKey = "mcp:publicUrl";
	public const String McpPath = "/mcp";

	public String Resource => Issuer + McpPath;
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.ComponentModel;

using ModelContextProtocol.Server;

using A2v10.Infrastructure;

namespace A2v10.Mcp.Tools;

[McpServerToolType]
public class WhoAmITool
{
	[McpServerTool(Name = "whoami"), Description("Returns the user this connection is signed in as.")]
	public static Object WhoAmI(ICurrentUser currentUser)
	{
		var ident = currentUser.Identity;
		return new { ident.Id, ident.Name, ident.PersonName, ident.Tenant };
	}
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Core.Web.Site.TestServices;

// The probe of the chain OAuth → ticket → ICurrentUser, with no database.
public class WhoAmITool(ICurrentUser _currentUser) : IPlatformMcpTool
{
	private static readonly JsonElement _schema = JsonDocument.Parse("""{"type":"object"}""").RootElement;

	public String Name => "whoami";
	public String Title => "Who am I";
	public PlatformMcpToolHints Hints => PlatformMcpToolHints.ReadOnly;
	public String Description => "Returns the user this connection is signed in as.";
	public JsonElement InputSchema => _schema;
	public String[]? Roles => null;

	public Task<Object?> ExecuteAsync(JsonElement args, CancellationToken cancellationToken)
	{
		var ident = _currentUser.Identity;
		return Task.FromResult<Object?>(new { ident.Id, ident.Name, ident.PersonName, ident.Tenant });
	}
}

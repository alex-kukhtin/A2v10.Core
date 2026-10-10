// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

/* The 'admin' namespace: the platform's screens for running the application, one endpoint per screen
 * (/admin/user, ...). System endpoints like the tag dialog - behaviour in code, no file, no shape.
 * What the namespace adds is one right for all of it: only an admin is served. It is checked where an
 * endpoint becomes a builder (ModelBuilderFactory), so a new screen cannot forget it.
 */
public abstract record AdminEndpointMetadata : EndpointMetadata
{
    // an unknown screen fails here, not as an endpoint with nothing behind it
    internal static AdminEndpointMetadata Create(String name) => name switch
    {
        UserAdminEndpointMetadata.ScreenName => new UserAdminEndpointMetadata()
        {
            Schema = Constants.SchemaNames.Admin,
            Name = name
        },
        _ => throw new InvalidOperationException($"'/{Constants.SchemaNames.Admin}/{name}' is not an admin screen")
    };
}

/* The users: the list, the card (profile and roles), a new user, a password. The account itself is
 * Identity's (IAppUserAdmin); this screen writes the profile and the roles.
 */
public sealed record UserAdminEndpointMetadata : AdminEndpointMetadata
{
    internal const String ScreenName = "user";

    internal const String IndexAction = "index";
    internal const String EditAction = "edit";
    internal const String CreateAction = "create";
    internal const String PasswordAction = "password";
}

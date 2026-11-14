// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

/* /_profile: the user's own page, at the address the shell's menu under the user's name opens.
 * The platform's, like the admin screens, and with no right to check: everyone has one and it
 * reaches no one else's row. Two parts: the account's (the name, the locale, the theme - Identity's,
 * IAppUserProfile) and the slots of profile/metadata.json (usr.Profile, keyed by the user). Without
 * the file the page has the first part only.
 */
public sealed record ProfilePageEndpointMetadata : EndpointMetadata
{
    internal const String IndexAction = Constants.Actions.Index;
}

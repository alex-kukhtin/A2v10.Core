// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace A2v10.Metadata;

// the keys of app.json the generator reads; the rest of the file belongs to the client
internal sealed record AppJsonMetadata
{
    public String? PlatformId { get; set; }
    public Dictionary<String, String[]>? Aliases { get; set; }
    // columns as tokens, not a record: a column the role does not have is refused, not dropped (AppRoles)
    public Dictionary<String, Dictionary<String, JToken?>?>? Roles { get; set; }
    public Boolean UseGrants { get; set; }
}

/* app.json as the generator reads it: read and checked once, cached whole (DatabaseMetadataCache) and
 * dropped whole by an edit of the file. One file is one read - a key added here is a field, not
 * another read and another cache.
 *
 * The platformid is the DECLARED one only: the base in force is the database's fact, checked against
 * this per data source (DatabaseMetadataProvider.LoadPlatformIdAsync).
 */
internal sealed record AppJson(
    KindFolders Folders,
    AppPlatformId? DeclaredPlatformId,
    IReadOnlyList<AppRole>? Roles,
    Boolean UseGrants)
{
    // no app.json - no aliases, no declared base, no roles, no grants
    internal static AppJson From(AppJsonMetadata? app) => new(
        KindFolders.From(app?.Aliases),
        app?.PlatformId is String name ? AppPlatformId.FromSqlName(name) : null,
        AppRoles.From(app?.Roles),
        app?.UseGrants ?? false);
}

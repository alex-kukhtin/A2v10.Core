// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace A2v10.Metadata;

/* The keys of app.json the generator reads; the rest of the file belongs to the client. A key added here
 * goes into WebAppDataProvider.SERVER_KEYS (A2v10.Platform.Web) too, or it rides to the browser.
 */
internal sealed record AppJsonMetadata
{
    public String? PlatformId { get; set; }
    public Dictionary<String, String[]>? Aliases { get; set; }
    // columns as tokens, not a record: a column the role does not have is refused, not dropped (AppRoles)
    public Dictionary<String, Dictionary<String, JToken?>?>? Roles { get; set; }
    public Boolean UseGrants { get; set; }
    public String[]? Boundary { get; set; }
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
    Boolean UseGrants,
    IReadOnlyList<String> Boundary)
{
    // no app.json - no aliases, no declared base, no roles, no grants, no boundary
    internal static AppJson From(AppJsonMetadata? app)
    {
        var folders = KindFolders.From(app?.Aliases);
        return new(
            folders,
            app?.PlatformId is String name ? AppPlatformId.FromSqlName(name) : null,
            AppRoles.From(app?.Roles),
            app?.UseGrants ?? false,
            BoundaryOf(app?.Boundary, folders));
    }

    /* 'boundary': the catalogs the user's boundary is cut by - "boundary": ["/catalog/store"]. Declared
     * only: nothing reads it yet (skill permissions.md, "Межа"). A dimension is a catalog - an
     * organizational unit, so the kind is asked through the aliases, as the loader asks it. Kept as the
     * endpoint's Path spells it, lower case.
     */
    private static IReadOnlyList<String> BoundaryOf(String[]? boundary, KindFolders folders)
    {
        List<String> paths = [];
        foreach (var path in boundary ?? [])
        {
            var (schema, table) = DatabaseMetadataProvider.ParsePath(path);
            if (String.IsNullOrEmpty(table) || folders.KindOf(schema) != Constants.SchemaNames.Catalog)
                throw new InvalidOperationException($"""
                    app.json: 'boundary' names '{path}', which is not a catalog.
                      A dimension of the boundary is a catalog of places people answer for: "boundary": ["/catalog/store"].
                    """);
            var normal = $"/{schema}/{table}";
            if (paths.Contains(normal))
                throw new InvalidOperationException($"app.json: 'boundary' names '{path}' twice");
            paths.Add(normal);
        }
        return paths;
    }
}

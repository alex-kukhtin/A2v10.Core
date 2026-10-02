// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

// one listed endpoint as the index knows it: no endpoint loaded, only files read
internal sealed record McpEntity(String Name, String Path, String Schema, String Table, String Kind, String[]? Roles,
    McpDeclarationMetadata? Mcp)
{
    internal Boolean IsCatalog => Kind == Constants.SchemaNames.Catalog;

    // ModelJson.CheckRoles: no roles is everyone; otherwise the user's must intersect. No implicit Admin.
    internal Boolean IsVisibleTo(IEnumerable<String>? userRoles) =>
        Roles == null || (userRoles != null && Roles.Intersect(userRoles).Any());
}

internal sealed record McpJsonFile
{
    public List<String> Endpoints { get; init; } = [];
}

/* mcp.json and what the index derives from it without loading an endpoint: the name (second segment),
 * the kind (the folder, through app.json aliases), the roles (the folder's model.json), the 'mcp' key of
 * the folder's metadata.json (parsed for that key alone - domain_info shows its description). Loaded at the
 * first tools/list - the schema of catalog_find enumerates the catalogs - and cached with the metadata.
 * Refused here, naming the path: a path that is not two segments, a kind other than catalog/document,
 * no metadata.json in the folder, two equal names. The rest is `a2 meta validate`'s mcp stage.
 */
internal sealed record McpIndex(IReadOnlyList<McpEntity> Entities)
{
    internal const String FileName = "mcp.json";

    internal McpEntity? ByPath(String path) => Entities.FirstOrDefault(e => e.Path == path);

    internal static async Task<McpIndex> LoadAsync(IAppCodeProvider code, Func<String, Task<String>> kindOf)
    {
        using var stream = code.FileStreamRO(FileName, primaryOnly: true)
            ?? throw new InvalidOperationException($"{FileName} not found in the application root: it lists the endpoints open to the model");
        using var sr = new StreamReader(stream);
        var file = JsonConvert.DeserializeObject<McpJsonFile>(await sr.ReadToEndAsync())
            ?? throw new InvalidOperationException($"{FileName} is empty");

        var entities = new List<McpEntity>();
        foreach (var path in file.Endpoints)
        {
            var (schema, table) = DatabaseMetadataProvider.ParsePath(path);
            if (String.IsNullOrEmpty(table))
                throw new InvalidOperationException($"{FileName}: '{path}' names no endpoint - a path is <kind>/<name>");
            var kind = await kindOf(schema);
            if (kind is not (Constants.SchemaNames.Catalog or Constants.SchemaNames.Document))
                throw new InvalidOperationException($"{FileName}: '{path}' is a {kind}; the model is shown catalogs and documents");
            entities.Add(new McpEntity(table, $"/{schema}/{table}", schema, table, kind, ReadRoles(code, schema, table),
                ReadMcp(code, path, schema, table)));
        }
        if (entities.GroupBy(e => e.Name).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new InvalidOperationException(
                $"{FileName}: {String.Join(" and ", twice.Select(e => e.Path))} give the model one name '{twice.Key}' - the name is the folder");
        return new McpIndex(entities);
    }

    static McpDeclarationMetadata? ReadMcp(IAppCodeProvider code, String path, String schema, String table)
    {
        using var stream = code.FileStreamRO($"{schema}/{table}/metadata.json")
            ?? throw new InvalidOperationException($"{FileName}: '{path}' has no metadata.json");
        using var sr = new StreamReader(stream);
        return JObject.Parse(sr.ReadToEnd())["mcp"]?
            .ToObject<McpDeclarationMetadata>(JsonSerializer.Create(JsonSettings.CamelCaseSerializerSettings));
    }

    // the root 'roles' of the folder's model.json - the same list the UI checks; no file, no roles
    static String[]? ReadRoles(IAppCodeProvider code, String schema, String table)
    {
        using var stream = code.FileStreamRO($"{schema}/{table}/model.json");
        if (stream == null)
            return null;
        using var sr = new StreamReader(stream);
        return JObject.Parse(sr.ReadToEnd())["roles"]?.ToObject<String[]>();
    }
}

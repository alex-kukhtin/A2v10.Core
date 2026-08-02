// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal sealed record PlatformIdType
{
    public String? DataType { get; set; }
}

public class DatabaseMetadataProvider(DatabaseMetadataCache _metadataCache, IDbContext _dbContext, IAppCodeProvider _codeProvider,
        SqlDbGenerator _sqlDbGenerator)
{
    public async Task CheckDeployAsync(String? dataSource)
    {
        if (!_metadataCache.IsMetadataDirty)
            return;

        var allMeta = await AllElementsMetadata(dataSource);

        var ok = await _sqlDbGenerator.CheckDeployAsync(dataSource, allMeta);
        if (ok)
            _metadataCache.ClearDirty();
    }

    /* Phase 2: the reference graph is built after the endpoint is published to the cache,
     * because it is cyclic - a re-entry has to find the instance, not build it again.
     */
    public async Task<EndpointMetadata> GetEndpointAsync(IModelBaseMeta meta, String? dataSource)
    {
        var endpoint = await GetEndpointAsync(dataSource, meta.CurrentSchema, meta.CurrentTable);
        if (endpoint is NormalEndpointMetadata normal)
            await ResolvePostedAsync(normal.Declaration, dataSource);
        return endpoint;
    }

    public async Task<EndpointMetadata> GetEndpointAsync(String? dataSource, String schema, String table)
    {
        var endpoint = await _metadataCache.GetOrAddAsync(dataSource, schema, table, LoadEndpointAsync);
        await ResolveReferencesAsync(endpoint, dataSource);
        return endpoint;
    }

    // a reference target must resolve to one of our tables, so a report is not a legal target
    public async Task<NormalEndpointMetadata> GetNormalEndpointAsync(String? dataSource, String schema, String table)
        => await GetEndpointAsync(dataSource, schema, table) as NormalEndpointMetadata
            ?? throw new InvalidOperationException($"Endpoint /{schema}/{table} is not a data endpoint");

    public async Task<TableMetadata> GetSchemaAsync(String? dataSource, String schema, String table)
        => (await GetNormalEndpointAsync(dataSource, schema, table)).Storage;

    public Task<AppMetadata> GetAppMetadataAsync(String? dataSource)
    {
        return _metadataCache.GetAppMetadataAsync(dataSource, LoadAppMetadataAsync);
    }

    internal Task<AppPlatformId> GetPlatformIdAsync(String? dataSource)
    {
        return _metadataCache.GetPlatformIdAsync(dataSource, LoadPlatformIdAsync);
    }

    public async Task<EndpointTableInfo> GetModelInfoFromPathAsync(String path)
    {
        var modelTableInfo = _metadataCache.GetModelInfoFromPath(path);
        if (modelTableInfo == null) {
            var (schema, table) = ParsePath(path);
            await GetEndpointAsync(null, schema, table);
            _metadataCache.GetOrAddEndpointPath(null, path, schema, table);
            modelTableInfo = _metadataCache.GetModelInfoFromPath(path);
        }
        if (modelTableInfo == null)
            throw new InvalidOperationException("GetModelInfo fails");
        return modelTableInfo;
    }
    public Task<UIElement> GetXamlFormAsync(String? dataSource, EndpointMetadata endpoint, String key, Func<UIElement> defForm)
    {
        return _metadataCache.GetOrAddXamlFormAsync(dataSource, endpoint, key, defForm);
    }

    public static IEnumerable<ReferenceMember> EnumFields(TableMetadata table, Boolean withDetails)
    {
        static ReferenceMember CreateMember(TableColumn column, Int32 index) => 
            new(column, MetadataExtensions.CreateEnumMeta(column), index);

        Int32 index = 0;
        var list = new List<ReferenceMember>();
        foreach (var cx in table.Columns.Where(c => c.IsEnum))
            list.Add(CreateMember(cx, index++));
        if (withDetails)
            foreach (var dt in table.Details.Select(x => x.Value))
                foreach (var ct in dt.Columns.Where(c => c.IsEnum))
                    list.Add(CreateMember(ct, index++));
        return list;
    }

    /* No default and no fallback on purpose. An absent answer means the 'platformid' type
     * is not in the database, and guessing a base here would not surface as a failure -
     * it would write values of the wrong shape into a live database, which is the one
     * outcome this whole arrangement exists to prevent.
     */
    private async Task<AppPlatformId> LoadPlatformIdAsync(String? dataSource)
    {
        var found = await _dbContext.LoadAsync<PlatformIdType>(dataSource, "a2meta.[GetPlatformIdType]");
        return AppPlatformId.FromSqlName(found?.DataType
            ?? throw new InvalidOperationException("a2meta.[GetPlatformIdType] returns nothing. The 'platformid' type is not defined in the database"));
    }

    private async Task<AppMetadata> LoadAppMetadataAsync(String? dataSource)
    {
        var dm = await _dbContext.LoadModelAsync(dataSource, "a2meta.[App.Metadata]")
            ?? throw new InvalidOperationException("a2meta.[App.Metadata] returns null");
        var appExpando = dm.Eval<ExpandoObject>("Application");
        var json = JsonConvert.SerializeObject(appExpando) ??
            throw new InvalidOperationException("AppMetadata not found");
        var meta = JsonConvert.DeserializeObject<AppMetadata>(json, JsonSettings.IgnoreNull)
            ?? throw new InvalidOperationException("AppMetadata deserialization fails");
        return meta;
    }

    /* The endpoint is built here, once, before it is published to the cache: its own
     * declaration comes from its own folder, and the shape it works on is resolved
     * through 'storage'. An endpoint that owns its shape gets Table and Declaration
     * equal - the same instance, not a copy.
     */
    /* The address as the author sees it - what goes into a message they have to act on, so
     * always the path they would open, never the internal (schema, table) pair.
     */
    private static String MetadataFileName(String schema, String table) =>
        Path.Combine(schema, table, "metadata.json");

    private async Task<(String Text, String? Hash)> ReadMetadataFileAsync(String schema, String table)
    {
        var fileName = MetadataFileName(schema, table);
        using var stream = _codeProvider.FileStreamRO(fileName);
        if (stream == null)
            return ("{}", null); // empty value
        using var sr = new StreamReader(stream);
        var text = await sr.ReadToEndAsync()
            ?? throw new InvalidOperationException($"{fileName} is empty");
        return (text, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
    }

    /* One of our tables, built from the file that declares it and defaulted from its own
     * folder - never from the folder of whoever asked for it.
     *
     * Takes the text instead of reading it, so that an endpoint which owns its storage builds
     * it from the same read: one file, one read, one hash. Two reads of one file are not only
     * wasted io - they can see two different contents and put a declaration and a shape that
     * never coexisted into the same endpoint.
     */
    private static TableMetadata BuildStorage(String schema, String table, String text, String? hash)
    {
        // declared in code, not in a file - defaults and address are applied the same way
        var storage = TableMetadataDefaults.SystemTable(schema, table)
            ?? JsonConvert.DeserializeObject<TableMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
            ?? throw new InvalidOperationException($"{MetadataFileName(schema, table)}: TableMetadata deserialization fails");
        storage.FileHash = hash;
        storage.SetDefaults(schema, table);
        return storage;
    }

    private async Task<TableMetadata> LoadStorageAsync(String? dataSource, String schema, String table)
    {
        var (text, hash) = await ReadMetadataFileAsync(schema, table);
        return BuildStorage(schema, table, text, hash);
    }

    public Task<TableMetadata> GetStorageAsync(String? dataSource, String schema, String table)
    {
        return _metadataCache.GetOrAddStorageAsync(dataSource, schema, table, LoadStorageAsync);
    }

    /* The endpoint is built here, once, before it is published to the cache: its own
     * declaration comes from its own file, and the table it works on comes from the storage
     * cache - the same instance for every endpoint that points at it.
     *
     * The file is read once. The text is deserialized twice, once per type; each picks up the
     * keys it declares, and a key both types declare ('inherit', 'table') is legitimately read
     * twice - see DeclarationMetadata.
     */
    private async Task<EndpointMetadata> LoadEndpointAsync(String? dataSource, String schema, String table)
    {
        var (text, hash) = await ReadMetadataFileAsync(schema, table);
        var declaration = JsonConvert.DeserializeObject<DeclarationMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
            ?? throw new InvalidOperationException($"{MetadataFileName(schema, table)}: DeclarationMetadata deserialization fails");

        CheckDataLocation(schema, table, declaration);

        /* An endpoint that owns its storage builds it from the text already in hand; one that
         * points elsewhere goes to the cache for that address. Both go through the same cache,
         * so every endpoint pointing at one table still gets one instance.
         */
        TableMetadata storage;
        if (declaration.HasOwnStorage)
            storage = await _metadataCache.GetOrAddStorageAsync(dataSource, schema, table,
                (_, s, t) => Task.FromResult(BuildStorage(s, t, text, hash)));
        else
        {
            var (storageSchema, storageTable) = ParsePath(declaration.Storage!);
            storage = await GetStorageAsync(dataSource, storageSchema, storageTable);
        }

        /* The only place that decides which kind of endpoint this is. The discriminator is the
         * folder, not a key in the file: a file cannot lie about what it is.
         */
        if (schema == Constants.SchemaNames.Report)
            return new ReportEndpointMetadata()
            {
                Kind = EndpointKindOf(schema),
                Schema = schema,
                Name = table,
                // the surface a report reads; today addressed by 'storage', by 'source' later
                Surface = storage,
                Report = JsonConvert.DeserializeObject<ReportMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
                    ?? throw new InvalidOperationException("ReportMetadata deserialization fails"),
                FileHash = hash
            };

        return new NormalEndpointMetadata()
        {
            Kind = EndpointKindOf(schema),
            Schema = schema,
            Name = table,
            Storage = storage,
            Declaration = declaration,
            FileHash = hash
        };
    }

    /* Folders the enum does not know yet ('report', 'autonum') resolve to Undefined rather
     * than throwing: every endpoint gets a container, and this is the single place that will
     * learn the new kinds.
     */
    private static EndpointKind EndpointKindOf(String schema)
    {
        return schema switch
        {
            Constants.SchemaNames.Catalog => EndpointKind.Catalog,
            Constants.SchemaNames.Document => EndpointKind.Document,
            Constants.SchemaNames.Journal => EndpointKind.Journal,
            _ => EndpointKind.Undefined
        };
    }

    /* Kinds whose endpoints work on a table of ours and therefore have to say which one.
     * Everything else falls through EndpointKindOf as Undefined and is not asked: platform
     * namespaces (operations, tags) declare their tables in code, autonum is a registry and not
     * a table endpoint, and a report owns no table at all - it names a surface it reads, which
     * is a different key under a different rule.
     */
    private static Boolean DeclaresDataLocation(String schema) =>
        EndpointKindOf(schema) != EndpointKind.Undefined;

    /* Where the data lives is declared, never guessed.
     *
     * 'storage' - working on a table declared elsewhere - exists for one kind only: a family of
     * operations over one document table. Every other endpoint owns its table, so its rule is
     * not a choice between two keys but simply 'name it'. Two rules, and which one applies is
     * decided by the folder, so neither can be reached by writing the wrong key.
     *
     * There used to be a default - an absent 'storage' under document/ meant the shared
     * doc.Documents - and it was the single place in the format where writing nothing meant
     * *someone else's* table, while everywhere else writing nothing means your own. Nothing
     * replaces it: both readings of an undeclared endpoint are plausible and the wrong one is
     * silent, so the file is asked instead of guessed at.
     *
     * A missing metadata.json and an empty {} arrive here as the same text and get the same
     * message on purpose: 'the file is empty' would say less than 'nothing says where the data
     * lives', and the fix is identical.
     */
    private static void CheckDataLocation(String schema, String table, DeclarationMetadata declaration)
    {
        if (!DeclaresDataLocation(schema))
            return;

        var hasTable = !String.IsNullOrEmpty(declaration.Table);
        var hasStorage = !String.IsNullOrEmpty(declaration.Storage);
        var file = MetadataFileName(schema, table);

        if (schema != Constants.SchemaNames.Document)
        {
            if (hasStorage)
                throw new InvalidOperationException(String.Join(Environment.NewLine,
                    $"{file}: declares 'storage', which only a document endpoint may do.",
                    "  'storage' shares one table across a family of operations; every other kind owns its table.",
                    "  Declare \"table\": \"<TableName>\" instead."));
            if (!hasTable)
                throw new InvalidOperationException(String.Join(Environment.NewLine,
                    $"{file}: does not declare 'table', so nothing says where the data lives.",
                    "  Add \"table\": \"<TableName>\".",
                    "  There is no default: a table name is never derived from the folder name."));
            return;
        }

        if (hasTable == hasStorage)
            throw new InvalidOperationException(hasTable
                ? String.Join(Environment.NewLine,
                    $"{file}: declares both 'table' and 'storage'. These are two different layouts:",
                    $"    \"table\":   \"{declaration.Table}\" - this document has its own table;",
                    $"    \"storage\": \"{declaration.Storage}\" - this document is an operation over a table declared elsewhere.",
                    "  Keep one.")
                : String.Join(Environment.NewLine,
                    $"{file}: declares neither 'table' nor 'storage', so nothing says where the data lives.",
                    "    \"table\":   \"<TableName>\" - if this document has its own table;",
                    "    \"storage\": \"document\"    - if it is an operation over a table declared elsewhere.",
                    "  There is no default: an absent 'table' is not a shared table and not a derived name."));
    }

    private async Task<TableMetadata> LoadTableMetadataDbAsync(String? dataSource, String schema, String table)
    {
        var prms = new ExpandoObject()
        {
            {"Schema", schema},
            {"Table", table},
        };
        String procedure = schema switch {
            "rep" => "a2meta.[Report.Schema]",
            "enm" => "a2meta.[Enum.Schema]",
            "op" => table switch {
                "operations" => "a2meta.[Operation.Schema]",
                _ => "a2meta.[Table.Schema]"
            },
            _ => "a2meta.[Table.Schema]"
        };
        var dm = await _dbContext.LoadModelAsync(dataSource, procedure, prms)
            ?? throw new InvalidOperationException("a2meta.[Table.Schema] returns null");
        var tableExpando = dm.Eval<ExpandoObject>("Table")
            ?? throw new InvalidOperationException($"Metadata for {schema}.{table} not found");
        var json = JsonConvert.SerializeObject(tableExpando) 
            ?? throw new InvalidOperationException("TableMetadata not found");
        var meta = JsonConvert.DeserializeObject<TableMetadata>(json, JsonSettings.IgnoreNull)
            ?? throw new InvalidOperationException("TableMetadata deserialization fails");
        return meta;
    }


    internal static (String schema, String table) ParsePath(String path)
    {
        path = path.RemoveHeadSlash();
        var split = path.ToLowerInvariant().Split('/');
        if (split.Length == 1)
            return (split[0], String.Empty);
        if (split.Length < 2 )
            throw new InvalidOperationException($"Invalid path: {path}");
        return (split[0], split[1]);
    }

    public async Task ResolvePostedAsync(DeclarationMetadata meta, String? dataSource)
    {
        if (meta.Post == null)
            return;
        foreach (var p in meta.Post)
        {
            var (schema, table) = ParsePath(p.Journal);
            var refJournal = await GetSchemaAsync(dataSource, schema, table);
            p.JournalTable = refJournal;
        }
    }

    public async Task ResolveReferencesAsync(EndpointMetadata endpoint, String? dataSource)
    {
        var meta = endpoint switch { NormalEndpointMetadata n => n.Storage, ReportEndpointMetadata r => r.Surface, _ => throw new InvalidOperationException($"Unknown endpoint {endpoint.Path}") };
        static IEnumerable<TableColumn> GetAllReferences(TableMetadata table)
        {
            return table.Columns.Where(c => c.IsRef)
                .Concat(table.Details.Values.SelectMany(GetAllReferences));
        }

        var allRefs = GetAllReferences(meta).GroupBy(x => x.Target);

        foreach (var group in allRefs)
        {
            var column = group.First();
            foreach (var gcol in group) {

                if (gcol.Type == ColumnType.Parent)
                {
                    // self! - and a report has no columns, so this can only be a data endpoint
                    gcol.RefTable = endpoint as NormalEndpointMetadata
                        ?? throw new InvalidOperationException($"{endpoint.Path} cannot have a Parent column");
                    continue;
                }
                else if (gcol.Type == ColumnType.Operation)
                {
                    gcol.RefTable = await GetNormalEndpointAsync(dataSource, Constants.SchemaNames.Operations, String.Empty);
                    continue;
                }
            }

            if (column.Target == null)
                continue;

            var (schema, table) = ParsePath(column.Target);
            var refMeta = await GetNormalEndpointAsync(dataSource, schema, table);
            foreach (var gcol in group)
                gcol.RefTable = refMeta;
        }
    }

    private async Task<IEnumerable<TableMetadata>> AllElementsMetadata(String? dataSource)
    {
        var allMeta = _codeProvider.EnumerateAllFilesRecursive("", "metadata.json");
        var tables = new List<TableMetadata>();
        foreach (var file in allMeta)
        {
            var endpointPath = Path.GetDirectoryName(file)?.NormalizeSlash();
            if (endpointPath == null)
                continue;
            var (schema, table) = ParsePath(endpointPath);
            if (schema == "autonum")
                continue; // TODO: skip other elements
            /* Only a data endpoint declares a table, and only one that does not point elsewhere:
             * a shared storage is deployed by the file that declares it, a report declares none.
             */
            if (await GetEndpointAsync(dataSource, schema, table) is not NormalEndpointMetadata endpoint)
                continue;
            if (!endpoint.Declaration.HasOwnStorage)
                continue;
            tables.Add(endpoint.Storage);
        }
        if (tables.Any(t => t.HasTags) && !tables.Any(t => t.IsTags))
            tables.Add(await GetStorageAsync(dataSource, Constants.SchemaNames.Tags, String.Empty));
        return tables;
    }
}

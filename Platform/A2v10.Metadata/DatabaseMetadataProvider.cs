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

        var platformId = await GetPlatformIdAsync(dataSource);
        await _sqlDbGenerator.CheckDeployAsync(dataSource, allMeta, platformId);
        _metadataCache.ClearDirty();
    }

    public async Task<DeployDatabaseResult> DeployDatabaseAllAsync(String? dataSource)
    {
        var platformId = await GetPlatformIdAsync(dataSource);
        var allMeta = await AllElementsMetadata(dataSource);
        return await _sqlDbGenerator.CheckDeployAsync(dataSource, allMeta, platformId);
    }

    public Task<EndpointMetadata> GetEndpointAsync(IModelBaseMeta meta, String? dataSource)
        => GetEndpointAsync(dataSource, meta.CurrentSchema, meta.CurrentTable);

    /* The entry point: no load is running yet, so the cache opens one and publishes it whole.
     * Everything below takes that load as a parameter, which is what tells the two roles apart -
     * see EndpointLoad.
     */
    public Task<EndpointMetadata> GetEndpointAsync(String? dataSource, String schema, String table)
    {
        return _metadataCache.GetOrLoadAsync(dataSource, schema, table, LoadAsync);
    }

    /* Build, then link, and between the two the endpoint is put into the load: the graph is
     * cyclic, so a descent that comes back around has to find the instance and return.
     */
    private async Task<EndpointMetadata> LoadAsync(EndpointLoad load, String? dataSource, String schema, String table)
    {
        var found = load.Find(dataSource, schema, table);
        if (found != null)
            return found;
        var endpoint = await LoadEndpointAsync(load, dataSource, schema, table);
        load.Add(dataSource, schema, table, endpoint);
        await ResolveReferencesAsync(load, endpoint, dataSource);
        return endpoint;
    }

    // a reference target must resolve to one of our tables, so a report is not a legal target
    public async Task<NormalEndpointMetadata> GetNormalEndpointAsync(String? dataSource, String schema, String table)
        => await GetEndpointAsync(dataSource, schema, table) as NormalEndpointMetadata
            ?? throw new InvalidOperationException($"Endpoint /{schema}/{table} is not a data endpoint");

    private async Task<NormalEndpointMetadata> GetNormalEndpointAsync(EndpointLoad load, String? dataSource, String schema, String table)
        => await LoadAsync(load, dataSource, schema, table) as NormalEndpointMetadata
            ?? throw new InvalidOperationException($"Endpoint /{schema}/{table} is not a data endpoint");

    public async Task<TableMetadata> GetSchemaAsync(String? dataSource, String schema, String table)
        => (await GetNormalEndpointAsync(dataSource, schema, table)).Storage;

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


    /* The endpoint is built here, once, before it is published to the cache: its own
     * declaration comes from its own folder, and the shape it works on is resolved
     * through 'storage'. An endpoint that owns its shape gets Table and Declaration
     * equal - the same instance, not a copy.
     */
    /* The address as the author sees it - what goes into a message they have to act on, so
     * always the path they would open, never the internal (schema, table) pair.
     */
    private static String MetadataFileName(String schema, String table) =>
        Path.Combine(schema, table, "metadata.json").NormalizeSlash();

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
        var storage = JsonConvert.DeserializeObject<TableMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
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

    /* Endpoints the platform serves itself - no file to read, no shape to build. The kind is set
     * literally and not through EndpointKindOf: that one answers 'what did the FOLDER declare',
     * and a folder declares nothing here. Teaching it this namespace would also make
     * DeclaresShapeSource demand a 'table' key from an endpoint that has no file to put it in.
     */
    private async Task<EndpointMetadata?> GetInternalEndpointAsync(String? dataSource, String schema, String table)
    {
        if (schema == Constants.SchemaNames.Tag)
            return new TagEndpointMetadata()
            {
                Kind = EndpointKind.Tags,
                Schema = schema,
                Name = table
            };
        if (schema == Constants.SchemaNames.Operation)
            return new OperationEndpointMetadata()
            {
                Kind = EndpointKind.Operation,
                Schema = schema,
                Name = table,
                // through the cache like every other storage: the instance is shared, not remade
                Storage = await _metadataCache.GetOrAddStorageAsync(dataSource, schema, table,
                    (_, _, _) => Task.FromResult(TableMetadataDefaults.OperationsTable()))
            };
        return null;
    }

    /* The endpoint is built here, once, before it is published to the cache: the table it works
     * on is the same instance for every endpoint that points at it, and its declaration is what
     * that table declares with this file's own declaration on top.
     *
     * The file is read once. The text is deserialized twice, once per type; each picks up the
     * keys it declares, and a key both types declare ('inherit', 'table') is legitimately read
     * twice - see DeclarationMetadata.
     */
    private async Task<EndpointMetadata> LoadEndpointAsync(EndpointLoad load, String? dataSource, String schema, String table)
    {
        var internalEndpoint = await GetInternalEndpointAsync(dataSource, schema, table);
        if (internalEndpoint != null)
            return internalEndpoint;

        var (text, hash) = await ReadMetadataFileAsync(schema, table);
        var declaration = JsonConvert.DeserializeObject<DeclarationMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
            ?? throw new InvalidOperationException($"{MetadataFileName(schema, table)}: DeclarationMetadata deserialization fails");

        CheckShapeSource(schema, table, declaration);

        /* An endpoint that owns its shape builds it from the text already in hand; one that points
         * elsewhere asks for the endpoint at that address, because a shared table comes with a
         * shared declaration - what /document says about its columns holds for every operation
         * over it. Both roads end in the same storage cache, so every endpoint pointing at one
         * table still gets one instance.
         *
         * A report takes the first half only - the shape it reads. It lays no behaviour over the
         * journal's, so the declaration fetched here reaches the normal endpoint below and nobody
         * else.
         *
         * The path is there for certain: a kind that may point elsewhere was made to say where by
         * the check above, and a kind that may not never leaves the first branch.
         */
        TableMetadata storage;
        DeclarationMetadata? storageDeclaration = null;
        if (declaration.HasOwnShape)
            storage = await _metadataCache.GetOrAddStorageAsync(dataSource, schema, table,
                (_, s, t) => Task.FromResult(BuildStorage(s, t, text, hash)));
        else
        {
            var (targetSchema, targetTable) = ParsePath(declaration.SharedShape!);
            CheckSharedShapeTarget(schema, table, declaration, targetSchema, targetTable);
            var targetEndpoint = await GetNormalEndpointAsync(load, dataSource, targetSchema, targetTable);
            CheckSharedShapeOwnsTable(schema, table, declaration, targetEndpoint);
            storage = targetEndpoint.Storage;
            storageDeclaration = targetEndpoint.Declaration;
        }

        /* The only place that decides which kind of endpoint this is. The discriminator is the
         * folder, not a key in the file: a file cannot lie about what it is.
         */
        return schema switch
        {
            Constants.SchemaNames.Report => new ReportEndpointMetadata()
                {
                    Kind = EndpointKindOf(schema),
                    Schema = schema,
                    Name = table,
                    // the shape a report reads, resolved from 'surface'. It owns none of it
                    Surface = storage,
                    Report = JsonConvert.DeserializeObject<ReportMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
                        ?? throw new InvalidOperationException("ReportMetadata deserialization fails"),
                    FileHash = hash
                },
            _ => new NormalEndpointMetadata()
                {
                    Kind = EndpointKindOf(schema),
                    Schema = schema,
                    Name = table,
                    Storage = storage,
                    // layered first, then read against the shape - both while the endpoint is built,
                    // so what leaves here is finished and nothing has to come back to it
                    Declaration = BakeDeclaration(MergeDeclaration(declaration, storageDeclaration), storage, schema, table),
                    FileHash = hash
                }
        };
    }

    /* Everything the bake can say is about the file being loaded, and not one of its messages can
     * name it: the bake is handed a table, never an address, and for an endpoint over a shared
     * table the two are different files - a message reading 'not found in /document' sends the
     * author to the wrong one.
     *
     * So the name is put on here, once, at the only level that knows both, rather than threaded
     * through six methods that would each have to remember to pass it on. Nothing loaded through
     * 'storage' is inside this try: that endpoint is built by its own call and carries its own
     * name already, so a message is never prefixed twice.
     */
    private static DeclarationMetadata BakeDeclaration(DeclarationMetadata declaration, TableMetadata storage,
        String schema, String table)
    {
        try
        {
            return declaration.Bake(storage);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{MetadataFileName(schema, table)}: {ex.Message}", ex);
        }
    }

    /* 'storage' and 'surface' both name another endpoint, and a shape is declared by the endpoint
     * that owns it. Both rules below say that, and they are two because they can be asked at
     * different moments: pointing at yourself is answerable from the path alone, before anything
     * is loaded, and it has to be - the descent would re-enter this very endpoint, which is not
     * in the cache yet.
     *
     * The remaining shape - a and b naming each other - is not caught: answering it means
     * loading b, which is the descent itself. It takes two files written to point at one
     * another, and it ends in a stack overflow rather than a message.
     *
     * The messages name the key the author wrote, never the other one: a report told to fix its
     * 'storage' would be told to fix something it does not have.
     */
    private static void CheckSharedShapeTarget(String schema, String table, DeclarationMetadata declaration,
        String targetSchema, String targetTable)
    {
        if (!String.Equals(schema, targetSchema, StringComparison.OrdinalIgnoreCase)
            || !String.Equals(table, targetTable, StringComparison.OrdinalIgnoreCase))
            return;
        var key = declaration.SharedShapeKey;
        var hint = key == "storage" ? ", or declare 'table' here instead" : "";
        throw new InvalidOperationException($"""
            {MetadataFileName(schema, table)}: '{key}' points at this endpoint itself.
            '{key}': "{declaration.SharedShape}"
            '{key}' names the endpoint that declares the shape, which is never the one declaring '{key}'.
            Point at that endpoint{hint}.
            """);
    }

    private static void CheckSharedShapeOwnsTable(String schema, String table, DeclarationMetadata declaration,
        NormalEndpointMetadata targetEndpoint)
    {
        if (targetEndpoint.Declaration.HasOwnShape)
            return;
        var key = declaration.SharedShapeKey;
        var targetKey = targetEndpoint.Declaration.SharedShapeKey;
        throw new InvalidOperationException($"""
            {MetadataFileName(schema, table)}: '{key}' points at {targetEndpoint.Path}, which declares '{targetKey}' itself.
            '{key}': '{declaration.SharedShape}' - here;
            '{targetKey}': '{targetEndpoint.Declaration.SharedShape}' - there.
            '{key}' is one hop: it names the endpoint that declares the shape, not another one pointing at it.
            Point at {targetEndpoint.Declaration.SharedShape} instead.
            """);
    }

    /* The two layers a declaration comes in: what the shared table declares about itself, and
     * what this endpoint declares on top of it. An endpoint owning its table has no layer below
     * and is returned untouched.
     *
     * The law is one sentence - mine wins - and the shape of the value decides at what
     * granularity: a map merges by key, a set unions, a scalar is all-or-nothing.
     *
     * 'forms' is a map whose value is all-or-nothing. A form is a tree and its nodes carry no
     * names, so there is nothing inside one to address, and the only unit that can be chosen is
     * the whole form: an operation writing its own 'edit' still shows the storage's 'index'.
     *
     * Written as 'own with', so only the keys named here are layered and everything else stays
     * the endpoint's own. What is deliberately not named:
     *
     *   'table'/'storage'/'surface' - where MY shape comes from. Inheriting them would produce a
     *                       declaration naming two of them at once, the one state CheckShapeSource
     *                       exists to make unreachable.
     *   'post'            - what the operation DOES. The table is shared, the act is not: two
     *                       operations over one table post in opposite directions, which is
     *                       most of why they are two.
     *   'printForms'      - the blanks, for the same reason: a blank is paper under one act, and
     *                       two operations over one table print different papers. Storage holds
     *                       the shape and has no act, so there is nothing there to inherit.
     */
    private static DeclarationMetadata MergeDeclaration(DeclarationMetadata own, DeclarationMetadata? storage)
    {
        if (storage == null)
            return own;
        return own with
        {
            InitialValues = MergeByKey(own.InitialValues, storage.InitialValues),
            Rules = RuleMetadata.Merge(own.Rules, storage.Rules),
            Kinds = MergeKinds(own.Kinds, storage.Kinds),
            Autonum = Mine(own.Autonum, storage.Autonum),
            Details = MergeDetails(own.Details, storage.Details),
            Forms = MergeByKey(own.Forms, storage.Forms)
        };
    }

    private static String? Mine(String? own, String? storage) =>
        String.IsNullOrEmpty(own) ? storage : own;

    private static Dictionary<String, T> MergeByKey<T>(Dictionary<String, T> own, Dictionary<String, T> storage)
    {
        if (storage.Count == 0)
            return own;
        var merged = new Dictionary<String, T>(storage);
        foreach (var (key, value) in own)
            merged[key] = value;
        return merged;
    }

    /* The merge law itself lives on RuleMetadata.Merge - it is asked on two axes (storage under
     * operation here, collection under row kind in DeclarationMetadata.RulesFor) and one law
     * with two implementations is one law that can drift.
     *
     * Kinds layer by kind key, and inside a kind by the same law: an operation refining the
     * rules of one kind says nothing about the others.
     */
    private static Dictionary<String, KindDeclarationMetadata> MergeKinds(
        Dictionary<String, KindDeclarationMetadata> own, Dictionary<String, KindDeclarationMetadata> storage)
    {
        if (storage.Count == 0)
            return own;
        var merged = new Dictionary<String, KindDeclarationMetadata>(storage);
        foreach (var (key, value) in own)
            merged[key] = new KindDeclarationMetadata()
            {
                Rules = storage.TryGetValue(key, out var below)
                    ? RuleMetadata.Merge(value.Rules, below.Rules)
                    : value.Rules
            };
        return merged;
    }

    /* Rows layer the same way rows are shaped: by detail name, which is a key of the shared
     * TableMetadata.Details, so the two sides are talking about the same collection.
     */
    private static Dictionary<String, DeclarationMetadata> MergeDetails(
        Dictionary<String, DeclarationMetadata> own, Dictionary<String, DeclarationMetadata> storage)
    {
        if (storage.Count == 0)
            return own;
        var merged = new Dictionary<String, DeclarationMetadata>(storage);
        foreach (var (key, value) in own)
            merged[key] = MergeDeclaration(value, storage.GetValueOrDefault(key));
        return merged;
    }

    /* The kind of an endpoint declared by a folder. Platform namespaces ('operations', 'tag')
     * and registries ('autonum') resolve to Undefined rather than throwing: their kind, where
     * they have one, is set on the table by TableMetadataDefaults, and every endpoint gets a
     * container either way. This is the single place that learns a new file-declared kind.
     *
     * MetadataExtensions.ToEndpointKind answers the same question and throws on the rest - one
     * law in two spellings, and this one is how it drifted: it stayed silent about 'report'
     * long after the enum had the value.
     */
    private static EndpointKind EndpointKindOf(String schema)
    {
        return schema switch
        {
            Constants.SchemaNames.Catalog => EndpointKind.Catalog,
            Constants.SchemaNames.Document => EndpointKind.Document,
            Constants.SchemaNames.Journal => EndpointKind.Journal,
            Constants.SchemaNames.Report => EndpointKind.Report,
            Constants.SchemaNames.Enum => EndpointKind.Enum,
            _ => EndpointKind.Undefined
        };
    }

    /* Kinds whose endpoints have to say where the shape they work on comes from. Everything else
     * falls through EndpointKindOf as Undefined and is not asked: platform namespaces (operations,
     * tag) declare their tables in code, and autonum is a registry, not a table endpoint.
     */
    private static Boolean DeclaresShapeSource(String schema) =>
        EndpointKindOf(schema) != EndpointKind.Undefined;

    /* Where the shape comes from is declared, never guessed.
     *
     * Three keys on one axis - 'table' (my own), 'storage' (a table declared elsewhere, which I
     * write to), 'surface' (a shape I only read) - and exactly one of them is legal per folder:
     * 'storage' for a family of operations over one document table, 'surface' for a report, which
     * owns no table and therefore never reaches deploy, 'table' for everyone else. Which rule
     * applies is decided by the folder, so writing the wrong key never moves an endpoint into
     * another rule - it is an error naming the rule it broke.
     *
     * There used to be a default - an absent 'storage' under document/ meant the shared
     * doc.Documents - and it was the single place in the format where writing nothing meant
     * *someone else's* table, while everywhere else writing nothing means your own. Nothing
     * replaces it: both readings of an undeclared endpoint are plausible and the wrong one is
     * silent, so the file is asked instead of guessed at. The same reasoning brought the report
     * in here: an unasked report built an empty shape out of its own file and reported nothing.
     *
     * A missing metadata.json and an empty {} arrive here as the same text and get the same
     * message on purpose: 'the file is empty' would say less than 'nothing says where the shape
     * comes from', and the fix is identical.
     */
    private static void CheckShapeSource(String schema, String table, DeclarationMetadata declaration)
    {
        if (!DeclaresShapeSource(schema))
            return;

        var hasTable = !String.IsNullOrEmpty(declaration.Table);
        var hasStorage = !String.IsNullOrEmpty(declaration.Storage);
        var hasSurface = !String.IsNullOrEmpty(declaration.Surface);
        var file = MetadataFileName(schema, table);

        if (schema == Constants.SchemaNames.Report)
        {
            if (hasTable || hasStorage)
            {
                var owning = hasTable ? "table" : "storage";
                throw new InvalidOperationException($"""
                    {file}: declares '{owning}', which a report may not do.
                      A report is a window into a shape declared elsewhere: it owns no table and writes to none.
                      Declare "surface": "<path to a journal>" instead.
                    """);
            }
            if (!hasSurface)
                throw new InvalidOperationException($"""
                    {file}: does not declare 'surface', so nothing says which shape this report reads.
                      Add "surface": "/journal/<name>".
                      There is no default: an absent 'surface' is not a shape of the report's own.
                    """);
            return;
        }

        if (hasSurface)
            throw new InvalidOperationException($"""
                {file}: declares 'surface', which only a report may do.
                  'surface' names a shape that is read and never written; every other kind works on data of its own.
                  Declare "table": "<TableName>" instead.
                """);

        if (schema != Constants.SchemaNames.Document)
        {
            if (hasStorage)
                throw new InvalidOperationException($"""
                    {file}: declares 'storage', which only a document endpoint may do.
                      'storage' shares one table across a family of operations; every other kind owns its table.
                      Declare "table": "<TableName>" instead.
                    """);
            if (!hasTable)
                throw new InvalidOperationException($"""
                    {file}: does not declare 'table', so nothing says where the data lives.
                      Add "table": "<TableName>".
                      There is no default: a table name is never derived from the folder name.
                    """);
            return;
        }

        if (hasTable == hasStorage)
            throw new InvalidOperationException(hasTable
                ? $"""
                    {file}: declares both 'table' and 'storage'. These are two different layouts:
                        "table":   "{declaration.Table}" - this document has its own table;
                        "storage": "{declaration.Storage}" - this document is an operation over a table declared elsewhere.
                      Keep one.
                    """
                : $"""
                    {file}: declares neither 'table' nor 'storage', so nothing says where the data lives.
                        "table":   "<TableName>" - if this document has its own table;
                        "storage": "document"    - if it is an operation over a table declared elsewhere.
                      There is no default: an absent 'table' is not a shared table and not a derived name.
                    """);
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

    /* The targets of 'post' are references like any other, so they are linked where the others
     * are: phase 2, through the same load - once, before publication, and cycle-safe (a journal's
     * Document column points back at the document). It used to run from the request pipeline,
     * after publication: it wrote into a container the loader declares immutable, on every request,
     * and left every entry point that is not a request holding null journals.
     *
     * The mapping is then built and dropped - the throw is what it is built for.
     */
    private async Task ResolvePostAsync(EndpointLoad load, EndpointMetadata endpoint, String? dataSource)
    {
        if (endpoint is not NormalEndpointMetadata normal)
            return;
        normal.Declaration.CheckPost(normal.Path);
        if (normal.Declaration.Post is not { Count: > 0 } post)
            return;

        async Task<TableMetadata> JournalAsync(String path)
        {
            var (schema, table) = ParsePath(path);
            return (await GetNormalEndpointAsync(load, dataSource, schema, table)).Storage;
        }

        foreach (var p in post)
        {
            if (!p.IsSql)
            {
                p.JournalTable = await JournalAsync(p.Journal!);
                continue;
            }
            // assigned, never appended: a second pass would otherwise double the list
            var journals = new List<TableMetadata>();
            foreach (var path in p.Journals)
                journals.Add(await JournalAsync(path));
            p.JournalTables = journals;
        }
        _ = new PostStatements(normal);
    }

    /* Phase 2, run once per endpoint by LoadAsync and by nobody else. Reachable targets descend
     * through the same load, so one that comes back around finds the instance and returns -
     * which is what terminates a cycle, and what a second call from outside would undo.
     */
    private async Task ResolveReferencesAsync(EndpointLoad load, EndpointMetadata endpoint, String? dataSource)
    {
        var meta = endpoint switch {
            NormalEndpointMetadata n => n.Storage,
            ReportEndpointMetadata r => r.Surface,
            // a shape with no declared columns: the walk below finds nothing and that is correct,
            // its only column is the operation code and it points at this very endpoint
            OperationEndpointMetadata o => o.Storage,
            // no shape, so no references - said by name, so an unknown subtype still fails loudly
            TagEndpointMetadata => null,
            _ => throw new InvalidOperationException($"Unknown endpoint {endpoint.Path}")
        };
        if (meta == null)
            return;

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
                    // a system endpoint, so not a data endpoint - asked for as a reference target
                    gcol.RefTable = await LoadAsync(load, dataSource, Constants.SchemaNames.Operation,
                            String.Empty) as IRefTarget
                        ?? throw new InvalidOperationException(
                            $"/{Constants.SchemaNames.Operation} is not a reference target");
                    continue;
                }
            }

            if (column.Target == null)
                continue;

            var (schema, table) = ParsePath(column.Target);
            var refMeta = await GetNormalEndpointAsync(load, dataSource, schema, table);
            foreach (var gcol in group)
                gcol.RefTable = refMeta;
        }

        CheckLiteralInitials(endpoint, meta);

        await ResolvePostAsync(load, endpoint, dataSource);
    }

    /* A literal initial value on an enum column names a code, and here - and only here - is the
     * first moment the codes are known: the set is the far half of a reference, linked just above.
     * Without this the typo is silent in the worst way: '@map' finds no row, the RefId resolves to
     * nothing, and a NEW card simply opens with an empty control.
     *
     * Only enums are checked, because only they declare their rows. A literal pointing at a catalog
     * names an identifier that exists in the database and not in any file.
     */
    private static void CheckLiteralInitials(EndpointMetadata endpoint, TableMetadata meta)
    {
        if (endpoint is not NormalEndpointMetadata normal)
            return;
        foreach (var (key, initial) in normal.Declaration.InitialValues)
        {
            if (initial.Source != InitialSource.Literal)
                continue;
            var column = meta.Columns.FirstOrDefault(c => c.Name == key && c.IsEnum);
            if (column == null)
                continue;
            var target = column.RefTableCheck.Storage;
            if (target.Values.Count == 0)
                continue;
            var value = target.Values.FirstOrDefault(v => v.Id == initial.Value)
                ?? throw new InvalidOperationException(
                    $"{endpoint.Path}: initial value '{initial.Value}' for '{key}' is not a value of {target.Path}");
            if (value.Void)
                throw new InvalidOperationException(
                    $"{endpoint.Path}: initial value '{initial.Value}' for '{key}' is void in {target.Path}");
        }
    }


    public Task<IEnumerable<TableReferrer>> GetTableReferrersAsync(String? dataSource, TableMetadata table)
    {
        return _metadataCache.GetTableReferrersAsync(dataSource, table, LoadTableReferrersAsync);
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
            if (!endpoint.Declaration.HasOwnShape)
                continue;
            tables.Add(endpoint.Storage);
        }
        return tables;
    }
    private async Task<IEnumerable<TableReferrer>> LoadTableReferrersAsync(String? dataSource, TableMetadata table)
    {
        var prms = new ExpandoObject()
        {
            {"Schema", table.SqlSchema},
            {"Table", table.Table},
        };
        return await _dbContext.LoadListAsync<TableReferrer>(dataSource, "a2meta.[GetFkReferrers]", prms)
            ?? throw new InvalidOperationException("a2meta.[GetFkReferrers] returns null");
    }
}

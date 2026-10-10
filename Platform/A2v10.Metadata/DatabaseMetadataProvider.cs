// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Data.SqlClient;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    // the request path: when the metadata may differ from the database, once, under the cache's gate
    public Task CheckDeployAsync(String? dataSource) =>
        _metadataCache.CheckDeployAsync(() => DeployDatabaseAllAsync(dataSource));

    public async Task<DeployDatabaseResult> DeployDatabaseAllAsync(String? dataSource)
    {
        var (tables, roles, grants, boundary, platformId) = await DeployInputsAsync(dataSource);
        return await _sqlDbGenerator.CheckDeployAsync(dataSource, tables, roles, grants, boundary, platformId);
    }

    // 'a2 meta deploy --full': the file only, executed inside full.sql
    public async Task WriteDeployDatabaseAllAsync(String? dataSource)
    {
        var (tables, roles, grants, boundary, platformId) = await DeployInputsAsync(dataSource);
        await _sqlDbGenerator.WriteDeployAsync(dataSource, tables, roles, grants, boundary, platformId);
    }

    /* Everything a deploy is handed, gathered once for its callers. The one place the database is
     * asked about the platform at all before the script runs: that a2meta is there, and that the base
     * it rests on is the declared one.
     */
    private async Task<(IEnumerable<TableMetadata> Tables, IReadOnlyList<AppRole>? Roles,
        IReadOnlyList<(String Endpoint, EndpointGrant Grant)> Grants, IReadOnlyList<BoundaryDimension> Boundary, AppPlatformId PlatformId)>
        DeployInputsAsync(String? dataSource)
    {
        await EnsureMetaAsync(dataSource);
        var platformId = await GetPlatformIdAsync(dataSource);
        await CheckPlatformIdAsync(dataSource, platformId);
        var (tables, grants) = await AllElementsMetadata(dataSource);
        return (tables, await DeclaredRolesAsync(), grants, await BoundaryAsync(dataSource), platformId);
    }

    /* Is the platform in the database? a2meta is created by a2v10_metadata.sql - by full.sql, never
     * by the metadata deploy - and all the deploy reads (the hash, the platformid) lives in it.
     */
    public async Task<Boolean> HasMetaAsync(String? dataSource)
    {
        using var dbConn = await _dbContext.GetDbConnectionAsync(dataSource);
        using var cmd = dbConn.CreateCommand() as SqlCommand
            ?? throw new InvalidOperationException("Invalid Database provider");
        cmd.CommandText = "select schema_id(N'a2meta')";
        return await cmd.ExecuteScalarAsync() is not (null or DBNull);
    }

    public async Task EnsureMetaAsync(String? dataSource)
    {
        if (!await HasMetaAsync(dataSource))
            throw new InvalidOperationException(
                "The database has no a2meta schema: the platform is not deployed to it yet. Run 'a2 meta deploy --full' right after the build of the host.");
    }

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

    internal Task<AppPlatformId> GetPlatformIdAsync(String? dataSource)
    {
        return _metadataCache.GetPlatformIdAsync(dataSource, LoadPlatformIdAsync);
    }

    public Task<UIElement> GetXamlFormAsync(String? dataSource, EndpointMetadata endpoint, String key, Func<UIElement> defForm)
    {
        return _metadataCache.GetOrAddXamlFormAsync(dataSource, endpoint, key, defForm);
    }

    /* The base an Id rests on. Declared in app.json, that is the answer and no database is read: a
     * load - and so a validate - types an Id from the declaration alone. Not declared, the database is
     * asked; it holds the type once the first deploy made it. Absent in both is an error: guessing a
     * base would not surface as a failure, it would write values of the wrong shape into a live
     * database. The two are compared where the database is read anyway, by the deploy
     * (CheckPlatformIdAsync): the base never changes, and a declaration disagreeing with it is refused
     * there - before, every cold load paid the round trip for that check.
     */
    private async Task<AppPlatformId> LoadPlatformIdAsync(String? dataSource)
    {
        if (await DeclaredPlatformIdAsync() is { } declared)
            return declared;
        return await DatabasePlatformIdAsync(dataSource)
            ?? throw new InvalidOperationException(
                "The 'platformid' type is not defined in the database, and app.json declares no 'platformid' to create it from");
    }

    private async Task<AppPlatformId?> DatabasePlatformIdAsync(String? dataSource) =>
        (await _dbContext.LoadAsync<PlatformIdType>(dataSource, "a2meta.[GetPlatformIdType]"))?.DataType is String fact
            ? AppPlatformId.FromSqlName(fact)
            : null;

    private async Task CheckPlatformIdAsync(String? dataSource, AppPlatformId platformId)
    {
        if (await DatabasePlatformIdAsync(dataSource) is { } fact && fact != platformId)
            throw new InvalidOperationException(
                $"app.json: 'platformid' is '{platformId.SqlTypeName}', but the database rests on '{fact.SqlTypeName}'. The base never changes.");
    }

    // internal for EndpointValidator: the declaration is the only answer a check that never reads the database can have
    internal async Task<AppPlatformId?> DeclaredPlatformIdAsync() =>
        (await AppJsonAsync()).DeclaredPlatformId;

    // rows of a platform table, not a table: they travel to the deploy beside the tables, not among them
    internal async Task<IReadOnlyList<AppRole>?> DeclaredRolesAsync() =>
        (await AppJsonAsync()).Roles;

    // the switch of the rights; read by the runtime only - the deploy writes Grants whatever it says
    internal async Task<Boolean> UseGrantsAsync() =>
        (await AppJsonAsync()).UseGrants;

    // the deploy makes a table per dimension, the users screen a tab per dimension
    internal async Task<IReadOnlyList<BoundaryDimension>> BoundaryAsync(String? dataSource)
    {
        List<BoundaryDimension> dimensions = [];
        foreach (var path in (await AppJsonAsync()).Boundary)
        {
            var (schema, table) = ParsePath(path);
            dimensions.Add(new BoundaryDimension(path, (await GetNormalEndpointAsync(dataSource, schema, table)).Storage));
        }
        return dimensions;
    }

    private Task<AppJson> AppJsonAsync() =>
        _metadataCache.GetAppJsonAsync(async () => AppJson.From(await ReadAppJsonAsync()));

    private async Task<AppJsonMetadata?> ReadAppJsonAsync()
    {
        using var stream = _codeProvider.FileStreamRO("app.json", primaryOnly: true);
        if (stream == null)
            return null;
        using var sr = new StreamReader(stream);
        return JsonConvert.DeserializeObject<AppJsonMetadata>(await sr.ReadToEndAsync());
    }

    /* The kind a folder is written for. The address stays the folder - the file is read from it,
     * the cache is keyed by it, messages name it - and only what the folder IS is asked here: every
     * decision the loader takes by the first segment takes it by this answer instead.
     */
    internal async Task<String> KindFolderAsync(String folder) =>
        (await AppJsonAsync()).Folders.KindOf(folder);

    // the endpoints open to the model - see Mcp/McpIndex
    internal Task<McpIndex> GetMcpIndexAsync() =>
        _metadataCache.GetMcpIndexAsync(() => McpIndex.LoadAsync(_codeProvider, KindFolderAsync));


    /* The endpoint is built here, once, before it is published to the cache: its own
     * declaration comes from its own folder, and the shape it works on is resolved
     * through 'storage'. An endpoint that owns its shape gets Table and Declaration
     * equal - the same instance, not a copy.
     */
    /* The address as the author sees it - what goes into a message they have to act on, so
     * always the path they would open, never the internal (schema, table) pair.
     */
    internal static String MetadataFileName(String schema, String table) =>
        Path.Combine(schema, table, "metadata.json").NormalizeSlash();

    private async Task<(String Text, String? Hash)> ReadMetadataFileAsync(String schema, String table)
    {
        var fileName = MetadataFileName(schema, table);
        using var stream = _codeProvider.FileStreamRO(fileName);
        if (stream == null)
            return ("{}", null); // empty value
        using var sr = new StreamReader(stream);
        var text = await sr.ReadToEndAsync();
        // a file of nothing is not '{}': there is nothing to parse, and 'deserialization fails' would send the author to look for a syntax error
        if (String.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"{fileName} is empty");
        return (text, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
    }

    /* One of our tables, built from the file that declares it and defaulted from its own
     * folder - never from the folder of whoever asked for it.
     *
     * Takes the text instead of reading it, so that an endpoint which owns its storage builds
     * it from the same read: one file, one read, one hash. Two reads of one file are not only
     * wasted io - they can see two different contents and put a declaration and a shape that
     * never coexisted into the same endpoint.
     *
     * The kind comes in beside the folder, not instead of it: under an alias the two differ, and the
     * table is defaulted from what the folder IS, while its Path is still where the file lies.
     */
    private async Task<TableMetadata> BuildStorageAsync(TableKind kind, String folderKind, String schema, String table, String text, String? hash)
    {
        var storage = JsonConvert.DeserializeObject<TableMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
            ?? throw new InvalidOperationException($"{MetadataFileName(schema, table)}: TableMetadata deserialization fails");
        storage.FileHash = hash;
        storage.SetDefaults(kind, folderKind, schema, table);
        CheckNames(storage, schema, table);
        CheckPlatformColumns(storage, schema, table);
        CheckCompanyColumn(storage, schema, table);
        CheckAccPlan(storage, schema, table);
        await RegistryRows.LoadAsync(storage, schema, table, _codeProvider);
        return storage;
    }

    /* Columns the platform declares itself and reads by their platform name: a key, the presentation
     * (SqlModelColumnName spells [Name] for a column of that type whatever it is called), a link the
     * platform emits, a stamp the statements write by name. An author column of such a type is a
     * second one standing under the first's name - refused rather than spelled wrong.
     */
    private static readonly ColumnType[] _platformTypes = [ColumnType.Id, ColumnType.NaturalKey, ColumnType.Name,
        ColumnType.Master, ColumnType.Parent, ColumnType.Folder, ColumnType.RowVersion, ColumnType.PlatformId,
        ColumnType.StampUser, ColumnType.StampDate, ColumnType.StampUserNull, ColumnType.StampDateNull];

    private static void CheckPlatformColumns(TableMetadata storage, String schema, String table)
    {
        foreach (var (where, columns) in new[] { ("", storage.Columns) }.Concat(storage.Details.Select(d => ($"details '{d.Key}': ", d.Value.Columns))))
            if (columns.FirstOrDefault(c => _platformTypes.Contains(c.Type)) is { } column)
                throw new InvalidOperationException(
                    $"{MetadataFileName(schema, table)}: {where}[{column.Name}] is '{column.Type}', a column the platform declares itself - it is in the record already, under its own name");
    }

    /* The company is found by type - the counter is split by it and '{p}' is read through it - so
     * two of them make which one a guess. A collection carries none: a row belongs to its record,
     * and the record to one company.
     */
    internal static void CheckCompanyColumn(TableMetadata storage, String schema, String table)
    {
        var file = MetadataFileName(schema, table);
        var companies = storage.AllColumns(c => c.Type == ColumnType.Company).ToList();
        if (companies.Count > 1)
            throw new InvalidOperationException(
                $"{file}: {String.Join(", ", companies.Select(c => $"[{c.Name}]"))} are all 'company'. A record belongs to one company; another reference to the same catalog is a 'ref'");
        foreach (var (key, details) in storage.Details)
            if (details.Columns.FirstOrDefault(c => c.Type == ColumnType.Company) is { } column)
                throw new InvalidOperationException(
                    $"{file}: [{column.Name}] in '{key}' is 'company'. A row belongs to its record, so the company is a field of the record");
    }

    /* A ledger posts against one chart, and nothing names it but this key - no default, and not the
     * ledger's folder name: ledger/national -> accplan/national is a coincidence, not a rule. Anywhere
     * else the key is refused rather than dropped. That the path is a chart is asked in phase 2, where
     * the target is linked (ResolveReferencesAsync).
     */
    private static void CheckAccPlan(TableMetadata storage, String schema, String table)
    {
        var file = MetadataFileName(schema, table);
        if (storage.Kind != TableKind.Ledger)
        {
            if (!String.IsNullOrEmpty(storage.AccPlan))
                throw new InvalidOperationException(
                    $"{file}: 'accplan' names the chart a ledger posts against; {schema}/ is not {Constants.SchemaNames.Ledger}/");
            return;
        }
        if (String.IsNullOrEmpty(storage.AccPlan))
            throw new InvalidOperationException(
                $"{file}: does not declare 'accplan'. A ledger posts against one chart of accounts: \"accplan\": \"/{Constants.SchemaNames.AccPlan}/<name>\"");
    }

    /* A name becomes a SQL identifier, a TS type or member and a step of a binding path - and only
     * the first of these is quoted. So the rule is what all of them accept: letters, digits and '_',
     * not starting with a digit; letters of any script, as a JS identifier allows. '$' falls out of
     * it and is why the rule exists: it separates what the platform adds to a name
     * (cat.[Agent$TagEntries]), and an author name holding one could spell that table - two CREATE
     * TABLEs collapsing into one, silently.
     *
     * Asked after SetDefaults, so a name derived from the folder is checked with the written ones.
     * The platform's own '$' tables are built in code and never pass through here.
     */
    private static void CheckNames(TableMetadata storage, String schema, String table)
    {
        var file = MetadataFileName(schema, table);

        void Check(String? name, String what)
        {
            if (String.IsNullOrEmpty(name))
                return;
            if ((Char.IsLetter(name[0]) || name[0] == '_') && name.All(ch => Char.IsLetterOrDigit(ch) || ch == '_'))
                return;
            throw new InvalidOperationException(
                $"{file}: '{name}' ({what}) - a name is letters, digits and '_', not starting with a digit. '$' separates what the platform adds: cat.[Agent$TagEntries]");
        }

        void CheckShape(TableMetadata t, String where)
        {
            Check(t.Table, $"{where}table");
            Check(t.Model, $"{where}model");
            foreach (var column in t.Columns)
                Check(column.Name, $"{where}fields");
        }

        Check(storage.Schema, "schema");
        CheckShape(storage, String.Empty);
        foreach (var (key, details) in storage.Details)
        {
            Check(key, "details");
            /* Refused and never defaulted: singularising the key is the guess SetDefaults refuses
             * to make with a folder name, and only the main table has a default at all. Unwritten,
             * it used to travel empty into the row type ('T'), into the type the rows are saved in
             * (doc.[.Meta.TableType]) and into the envelope ([Rows!T!Array]) - all silently.
             */
            if (String.IsNullOrEmpty(details.Model))
                throw new InvalidOperationException(
                    $"{file}: details '{key}' does not declare 'model'. It names the row type (T<model>) and the type its rows are saved in");
            CheckShape(details, $"details.{key}.");
            foreach (var kind in details.Kinds.Keys)
                Check(kind, $"details.{key}.kinds");
        }

        /* Legal names, composed, may still meet: a type is T{kind}{model}, a row set's array is
         * {kind}{key}, and neither composition knows the others. Two shapes under one type name, or
         * two things under one member of the record, reach the model and the .d.ts as one - silently,
         * the second wins. So the check is on what lands, not on what was written: a repeated
         * 'model' is one way to collide, a kind spelling another collection's name is the other.
         */
        static String RowSetOf(String key, String? kind) =>
            kind == null ? $"details '{key}'" : $"details '{key}' (kind '{kind}')";

        void Unique(IEnumerable<(String Name, String Source)> landed, String what)
        {
            foreach (var g in landed.GroupBy(x => x.Name).Where(g => g.Count() > 1))
                throw new InvalidOperationException(
                    $"{file}: {String.Join(" and ", g.Select(x => x.Source))} all produce {what} '{g.Key}' - one name, two things under it");
        }

        Unique([
            (storage.TypeName, "the record"),
            .. storage.Details.SelectMany(d => d.Value.RowSets().Select(rs => (rs.Type, RowSetOf(d.Key, rs.Kind))))
        ], "type");

        // the members of the record object: its columns, an array per row set, and the tags slot
        List<(String Name, String Source)> members = [
            .. storage.AllColumns().Select(c => (c.ModelName, $"field '{c.Name}'")),
            .. storage.Details.SelectMany(d => d.Value.RowSets().Select(rs => (rs.Collection, RowSetOf(d.Key, rs.Kind))))
        ];
        if (storage.HasTags)
            members.Add((Constants.FieldNames.Tags, "the tags trait"));
        Unique(members, $"member of {storage.Model}");
    }

    /* Endpoints the platform serves itself - no file to read, no shape to build, told apart by their
     * type. Recognised before the folder is asked what it declares, since it declares nothing.
     */
    private static async Task<EndpointMetadata?> GetInternalEndpointAsync(EndpointLoad load, String? dataSource, String schema, String table)
    {
        if (schema == Constants.SchemaNames.Tag)
            return new TagEndpointMetadata()
            {
                Schema = schema,
                Name = table
            };
        if (schema == Constants.SchemaNames.Operation)
            return new OperationEndpointMetadata()
            {
                Schema = schema,
                Name = table,
                // through the load like every other storage: the instance is shared, not remade
                Storage = await load.StorageAsync(dataSource, schema, table,
                    () => Task.FromResult(TableMetadataDefaults.OperationsTable()))
            };
        if (schema == Constants.SchemaNames.Admin)
            return AdminEndpointMetadata.Create(table);
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
        var internalEndpoint = await GetInternalEndpointAsync(load, dataSource, schema, table);
        if (internalEndpoint != null)
            return internalEndpoint;

        var (text, hash) = await ReadMetadataFileAsync(schema, table);
        var declaration = JsonConvert.DeserializeObject<DeclarationMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
            ?? throw new InvalidOperationException($"{MetadataFileName(schema, table)}: DeclarationMetadata deserialization fails");

        /* What the folder IS decides everything below; a folder that is nothing to the platform is
         * refused here with its name, not three calls later as a table with no baseline.
         */
        var kind = await KindFolderAsync(schema);
        if (!kind.IsFolderKind())
            throw new InvalidOperationException(
                $"{MetadataFileName(schema, table)}: '{schema}/' is not a kind of endpoint. A first segment is a kind ({String.Join(", ", Enum.GetNames<TableKind>().Take(8).Select(k => k.ToLowerInvariant()))}, report) or an alias of one (app.json 'aliases')");
        CheckShapeSource(kind, schema, table, declaration);
        if (kind == Constants.SchemaNames.Document)
            declaration = await LoadOperationsAsync(schema, table, declaration);

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
            // a report never owns a shape (CheckShapeSource), so the folder's kind is a table's here
            storage = await load.StorageAsync(dataSource, schema, table,
                () => BuildStorageAsync(kind.ToTableKind()!.Value, kind, schema, table, text, hash));
        else
        {
            var (targetSchema, targetTable) = ParsePath(declaration.SharedShape!);
            CheckSharedShapeTarget(schema, table, declaration, targetSchema, targetTable);
            var targetEndpoint = await GetNormalEndpointAsync(load, dataSource, targetSchema, targetTable);
            CheckSharedShapeOwnsTable(schema, table, declaration, targetEndpoint);
            storage = targetEndpoint.Storage;
            storageDeclaration = targetEndpoint.Declaration;
        }
        if (kind == Constants.SchemaNames.Document)
            CheckOperationColumn(MetadataFileName(schema, table), declaration, storage);

        var grants = EndpointGrants.From(MetadataFileName(schema, table), declaration.Grants);

        /* The only place that decides which kind of endpoint this is. The discriminator is the
         * folder, not a key in the file: a file cannot lie about what it is. An alias names the
         * folder's kind in app.json, which is the same answer given one step away.
         */
        return kind switch
        {
            Constants.SchemaNames.Report => new ReportEndpointMetadata()
                {
                    Schema = schema,
                    Name = table,
                    // the shape a report reads, resolved from 'surface'. It owns none of it
                    Surface = storage,
                    Report = JsonConvert.DeserializeObject<ReportMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
                        ?? throw new InvalidOperationException($"{MetadataFileName(schema, table)}: ReportMetadata deserialization fails"),
                    FileHash = hash,
                    Grants = grants
                },
            _ => new NormalEndpointMetadata()
                {
                    Schema = schema,
                    Name = table,
                    Storage = storage,
                    // layered first, then read against the shape - both while the endpoint is built,
                    // so what leaves here is finished and nothing has to come back to it
                    Declaration = BakeDeclaration(MergeDeclaration(declaration, storageDeclaration), storage, schema, table,
                        await GetPlatformIdAsync(dataSource)),
                    FileHash = hash,
                    Grants = grants
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
        String schema, String table, AppPlatformId platformId)
    {
        try
        {
            CheckAutonumColumn(declaration, storage);
            return declaration.Bake(storage, platformId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{MetadataFileName(schema, table)}: {ex.Message}", ex);
        }
    }

    /* 'autonum' says which numbering to draw from; the column of that type is where the number is
     * written. One without the other is unusable in a way nothing downstream reports - the save has
     * nowhere to put what it was given. The reverse is legitimate and is not checked: a column with
     * no numbering is a document numbered by hand.
     *
     * Called from inside the bake's try, which is what puts the file name on the message.
     */
    private static void CheckAutonumColumn(DeclarationMetadata declaration, TableMetadata storage)
    {
        if (String.IsNullOrEmpty(declaration.Autonum))
            return;
        if (storage.AllColumns().Any(c => c.Type == ColumnType.Autonum))
            return;
        throw new InvalidOperationException(
            $"'autonum' names the numbering '{declaration.Autonum}', but {storage.Path} declares no column of type 'autonum' for the number to land in");
    }

    internal const String OperationFileSuffix = ".operation.json";

    /* A document's operations: every name in 'operations' is a file '<name>.operation.json' beside
     * metadata.json, and every such file is a name in the list. Both ways - a name without a file is
     * an operation that cannot post, a file nobody lists is one that silently does not exist. Read
     * here, with the document's own text, so the endpoint is published whole.
     */
    private async Task<DeclarationMetadata> LoadOperationsAsync(String schema, String table, DeclarationMetadata declaration)
    {
        var folder = Path.Combine(schema, table).NormalizeSlash();
        var files = _codeProvider.EnumerateAllFiles(folder, $"*{OperationFileSuffix}")
            .Select(f => Path.GetFileName(f)[..^OperationFileSuffix.Length])
            .ToList();
        CheckOperations(MetadataFileName(schema, table), table, declaration, files);
        if (declaration.Operations.Count == 0)
            return declaration;

        var operations = new List<OperationDeclaration>();
        foreach (var name in declaration.Operations)
        {
            var fileName = Path.Combine(folder, $"{name}{OperationFileSuffix}").NormalizeSlash();
            using var stream = _codeProvider.FileStreamRO(fileName)
                ?? throw new InvalidOperationException($"{fileName}: not found");
            using var sr = new StreamReader(stream);
            var operation = JsonConvert.DeserializeObject<OperationFileMetadata>(await sr.ReadToEndAsync(),
                JsonSettings.CamelCaseSerializerSettings)
                ?? throw new InvalidOperationException($"{fileName}: OperationFileMetadata deserialization fails");
            operations.Add(ToOperationDeclaration(fileName, table, name, operation));
        }
        return declaration with { OperationDeclarations = operations };
    }

    /* What 'operations' says in metadata.json, checked against the files that lie beside it. The
     * storage (/document, no name of its own) is every document, not one act, so it has none.
     */
    internal static void CheckOperations(String file, String table, DeclarationMetadata declaration, IReadOnlyCollection<String> files)
    {
        var names = declaration.Operations;
        if (names.Count > 0 && String.IsNullOrEmpty(table))
            throw new InvalidOperationException(
                $"{file}: declares 'operations', and this is the storage - every document, not one of them. Declare them on the document that is opened from the menu.");
        foreach (var name in names)
            if (!IsIdentifier(name))
                throw new InvalidOperationException(
                    $"{file}: operation '{name}' - a name is letters, digits and '_', not starting with a digit: it is a file name, a url parameter and the tail of a code");
        if (names.GroupBy(n => n).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new InvalidOperationException($"{file}: operation '{twice.Key}' is listed twice");
        if (names.Count > 0 && declaration.Post != null)
            throw new InvalidOperationException(
                $"{file}: declares both 'operations' and 'post'. Each operation posts in its own way, so 'post' is written in each {OperationFileSuffix} and not here.");
        if (names.FirstOrDefault(n => !files.Contains(n)) is { } noFile)
            throw new InvalidOperationException(
                $"{file}: operation '{noFile}' has no file. Add '{noFile}{OperationFileSuffix}' beside metadata.json.");
        if (files.FirstOrDefault(f => !names.Contains(f)) is { } noName)
            throw new InvalidOperationException(names.Count == 0
                ? $"{file}: '{noName}{OperationFileSuffix}' lies beside it, and it declares no 'operations'. List it there, or remove the file."
                : $"{file}: '{noName}{OperationFileSuffix}' lies beside it and is not in 'operations': [{String.Join(", ", names)}]. List it, or remove the file.");
    }

    private static Boolean IsIdentifier(String name) =>
        name.Length > 0 && !Char.IsDigit(name[0]) && name.All(c => Char.IsLetterOrDigit(c) || c == '_');

    // 'post' is required: an operation IS what posting does, so one without it is a switch that changes nothing
    internal static OperationDeclaration ToOperationDeclaration(String fileName, String document, String name, OperationFileMetadata operation)
    {
        if (operation.Post is not { Count: > 0 } post)
            throw new InvalidOperationException($"{fileName}: declares no 'post'. What posting does is the whole of an operation.");
        if (operation.Properties.Count > 0
            || operation.Details.Values.Any(d => d.Properties.Count > 0 || d.Kinds.Values.Any(k => k.Properties.Count > 0)))
            throw new InvalidOperationException(
                $"{fileName}: declares 'properties'. One type serves every operation of the document, so a property of one operation is a test of the Operation column, and that is not generated yet. Put them on the document for now.");
        // the rules become 'when' under the operation's code, and are checked as such by the bake
        return new OperationDeclaration(name, $"{document}.{name}", post, operation.Rules, operation.Details);
    }

    /* Which rows of the table are this document's is told by the column of type 'operation', so every
     * rule that needs to tell them apart needs that column - and one that has it must use it.
     *
     *  - 'operations' writes a code per document into it;
     *  - 'storage' shares the table with other documents, and the code is what separates their rows:
     *    a storage without the column mixes them, and nothing can sort them out again;
     *  - a document with the column, its own table and its own 'post' but no 'operations' puts one
     *    value into it forever: the column carries nothing, and it reads as a missing 'operations';
     *  - the column is never an initial value: where a new document starts is the list and the url.
     */
    internal static void CheckOperationColumn(String file, DeclarationMetadata declaration, TableMetadata storage)
    {
        var column = storage.AllColumns().FirstOrDefault(c => c.IsOperation);
        if (column == null)
        {
            if (declaration.Operations.Count > 0)
                throw new InvalidOperationException(
                    $"{file}: declares 'operations', and {storage.Path} has no field of type 'operation' to hold the code. Add one to its 'fields'.");
            if (!String.IsNullOrEmpty(declaration.Storage))
                throw new InvalidOperationException(
                    $"{file}: 'storage' points at {storage.Path}, which has no field of type 'operation'. The rows of the documents over one table are told apart by it; add one to the 'fields' of {storage.Path}.");
            return;
        }
        if (declaration.HasOwnShape && declaration.Operations.Count == 0 && declaration.Post != null)
            throw new InvalidOperationException(
                $"{file}: has a field of type 'operation' and a 'post' of its own, and no 'operations' - the field would hold one value forever. Add 'operations', or remove the field.");
        if (declaration.InitialValues.ContainsKey(column.Name))
            throw new InvalidOperationException(
                $"{file}: 'initialValues' names [{column.Name}], the operation. Where a new document starts is the first of 'operations', or the one '?Op=' names.");
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
     *   'basedOn'         - what may be created from this document: a command of its screen, and
     *                       two documents over one table give rise to different ones.
     *   'grants'          - the rights on an address, and every document over the storage is an
     *                       address of its own. Read off the endpoint's file before this merge.
     */
    private static DeclarationMetadata MergeDeclaration(DeclarationMetadata own, DeclarationMetadata? storage)
    {
        if (storage == null)
            return own;
        return own with
        {
            InitialValues = MergeByKey(own.InitialValues, storage.InitialValues),
            Rules = RuleMetadata.Merge(own.Rules, storage.Rules),
            Properties = RuleMetadata.ByKey(own.Properties, storage.Properties),
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
            merged[key] = storage.TryGetValue(key, out var below)
                ? new KindDeclarationMetadata()
                {
                    Rules = RuleMetadata.Merge(value.Rules, below.Rules),
                    Properties = RuleMetadata.ByKey(value.Properties, below.Properties)
                }
                : value;
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

    /* Where the shape comes from is declared, never guessed.
     *
     * Three keys on one axis - 'table' (my own), 'storage' (a table declared elsewhere, which I
     * write to), 'surface' (a shape I only read) - and which are legal is decided per folder:
     * 'table' or 'storage' for the kinds that render (document, catalog, journal - an operation,
     * or a second address with screens of its own over the same rows), 'surface' for a report,
     * which owns no table and therefore never reaches deploy, 'table' alone for a set, nothing at
     * all for the numbering registry, whose names default. Writing the wrong key never moves an
     * endpoint into another rule - it is an error naming the rule it broke.
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
     *
     * The rules are asked by kind and the messages name the folder: under an alias the two differ.
     */
    private static void CheckShapeSource(String kind, String schema, String table, DeclarationMetadata declaration)
    {
        var hasTable = !String.IsNullOrEmpty(declaration.Table);
        var hasStorage = !String.IsNullOrEmpty(declaration.Storage);
        var hasSurface = !String.IsNullOrEmpty(declaration.Surface);
        var file = MetadataFileName(schema, table);

        /* One registry at one address: its names default and a written 'table' wins (SetDefaults).
         * Nothing to point elsewhere with - a second address to it would address nothing, as for a
         * set, and it reads no shape but its own.
         */
        if (kind == Constants.SchemaNames.Autonum)
        {
            if (hasStorage || hasSurface)
                throw new InvalidOperationException($"""
                    {file}: declares '{(hasStorage ? "storage" : "surface")}', which {kind}/ may not do.
                      The numbering registry has one address and its table defaults; the numberings are declared under 'autonums'.
                    """);
            return;
        }

        if (kind == Constants.SchemaNames.Report)
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

        /* The kinds that render: a second endpoint over one table is a second window on the same
         * rows - an operation of a document, a catalog or a journal with its own screens and its own
         * address. A set has no screen, so a second address to it would be an address to nothing.
         */
        if (kind is not (Constants.SchemaNames.Document or Constants.SchemaNames.Catalog or Constants.SchemaNames.Journal))
        {
            if (hasStorage)
                throw new InvalidOperationException($"""
                    {file}: declares 'storage', which '{kind}/' may not do.
                      'storage' is a second endpoint over one table, with screens of its own; a set has no screens.
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
                        "table":   "{declaration.Table}" - this endpoint has its own table;
                        "storage": "{declaration.Storage}" - this endpoint is a second one over a table declared elsewhere.
                      Keep one.
                    """
                : $"""
                    {file}: declares neither 'table' nor 'storage', so nothing says where the data lives.
                        "table":   "<TableName>"     - if this endpoint has its own table;
                        "storage": "/{kind}/<name>" - if it is a second one over a table declared elsewhere (a document over a shared table, a second screen).
                      There is no default: an absent 'table' is not a shared table and not a derived name.
                    """);
    }

    /* The address of an endpoint: the folder, two segments. One segment is a registry with no name
     * of its own (/autonum, /operation). A third is refused, never dropped: it used to be cut off
     * silently, and while only paths the platform composed from a metadata.json folder came here
     * that was unreachable - now a 'model: $meta' written by hand in a deeper folder comes here too,
     * and truncating it would serve another endpoint's data under this one's screen.
     */
    internal static (String schema, String table) ParsePath(String path)
    {
        var split = path.RemoveHeadSlash().ToLowerInvariant().Split('/', StringSplitOptions.RemoveEmptyEntries);
        return split.Length switch
        {
            1 => (split[0], String.Empty),
            2 => (split[0], split[1]),
            _ => throw new InvalidOperationException(
                $"'{path}': the metadata layer is addressed by two segments, <kind>/<name>; this path has {split.Length}")
        };
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
        var post = normal.Declaration.Posts().SelectMany(p => p.Post).ToList();
        if (post.Count == 0)
            return;

        async Task<TableMetadata> JournalAsync(String path)
        {
            var (schema, table) = ParsePath(path);
            return (await GetNormalEndpointAsync(load, dataSource, schema, table)).Storage;
        }

        /* The key names the kind of its target, so a path of the other kind is refused here - past this
         * point a ledger under 'journal' fails as a column nobody can resolve, far from the cause.
         * 'journals' of a procedure takes both: what reads a procedure's result (the provenance delete,
         * the transactions dialog) counts tables, and a ledger is one.
         */
        /* An account literal in a leg is referenced by code, and the code is in the chart's file - so it
         * is checked here, without a database, like an autonum name. Only the file: code does not refer
         * to an account a user adds. The rest of the legs is checked by PostStatements below.
         */
        async Task CheckConstAccountsAsync(PostMetadata p)
        {
            var ledger = p.TargetTableCheck;
            foreach (var (leg, blocks) in new[] { ("dt", p.Dt!), ("ct", p.Ct!) })
                foreach (var (name, code) in blocks.Const)
                {
                    if (ledger.AllColumns().FirstOrDefault(c => c.Name == name && c.Type == ColumnType.Account) is not { } column)
                        continue;
                    var chart = await JournalAsync(column.Target!);
                    var account = chart.Rows.FirstOrDefault(r => r.Id == code)
                        ?? throw new InvalidOperationException(
                            $"post: {normal.Path} -> {ledger.Path}: '{leg}' const [{name}] = '{code}' is not an account of {chart.Path}");
                    CheckSplitLeg($"post: {normal.Path} -> {ledger.Path}", leg, blocks, account);
                }
        }

        async Task<TableMetadata> TargetAsync(String key, String path, params TableKind[] kinds)
        {
            var target = await JournalAsync(path);
            if (!kinds.Contains(target.Kind))
                throw new InvalidOperationException(
                    $"post: {normal.Path}: '{key}' names {path}, which is a {target.Kind}, not a {String.Join(" or ", kinds)}");
            return target;
        }

        foreach (var p in post)
        {
            if (!p.IsSql)
            {
                p.TargetTable = p.IsLedger
                    ? await TargetAsync("ledger", p.Ledger!, TableKind.Ledger)
                    : await TargetAsync("journal", p.Journal!, TableKind.Journal);
                if (p.IsLedger)
                    await CheckConstAccountsAsync(p);
                continue;
            }
            // assigned, never appended: a second pass would otherwise double the list
            var journals = new List<TableMetadata>();
            foreach (var path in p.Journals)
                journals.Add(await TargetAsync("journals", path, TableKind.Journal, TableKind.Ledger));
            p.SqlTargets = journals;
        }
        _ = new PostStatements(normal);
    }

    /* A leg onto a Split account fills every column the account is split by: a payment leg without
     * the Agent a sale leg carries lands at another object, and the two never net - a receivable and
     * a payable that are one debt. Only a literal account is known here; one taken from a field of
     * the document is the Account domain's, not built.
     */
    internal static void CheckSplitLeg(String head, String leg, PostLegMetadata blocks, SeedRow account)
    {
        var named = blocks.Const.Keys.Concat(blocks.Document.Keys).Concat(blocks.Row.Keys).ToHashSet();
        var missing = account.SplitBy.Where(c => !named.Contains(c)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"{head}: '{leg}' posts to '{account.Id}', which is split by {String.Join(", ", account.SplitBy.Select(c => $"[{c}]"))}, and names no {String.Join(", ", missing.Select(c => $"[{c}]"))}");
    }

    /* 'basedOn' names other documents, so it is resolved where references are: phase 2, through the
     * same load. The target is a document; its operation is named exactly when it lists some, and is
     * one of them; a target is offered once; what is copied can be (BasedOnMapping) - asked in the
     * direction the file points, from this document to the target's columns. Where the basis lands in
     * the target is the target's question, asked at birth - it knows its source from the url.
     */
    private async Task ResolveBasedOnAsync(EndpointLoad load, EndpointMetadata endpoint, String? dataSource)
    {
        if (endpoint is not NormalEndpointMetadata normal)
            return;
        var declaration = normal.Declaration;
        foreach (var b in declaration.BasedOn)
        {
            if (String.IsNullOrEmpty(b.Target))
                throw new InvalidOperationException($"basedOn: {normal.Path}: an entry declares no 'target'");
            var head = $"basedOn: {normal.Path} -> {b.Target}";
            var (schema, table) = ParsePath(b.Target);
            var target = await GetNormalEndpointAsync(load, dataSource, schema, table);
            if (target.Storage.Kind != TableKind.Document)
                throw new InvalidOperationException($"{head}: a {target.Storage.Kind}, and only a document is created on basis");
            CheckBasedOnOperation(head, b, target);
            BasedOnMapping.CheckDocument(head, b, target.Storage, normal.Storage);
            BasedOnMapping.Rows(head, b, target.Storage, normal.Storage);
            b.TargetEndpoint = target;
        }
        CheckOfferedOnce(normal.Path, declaration);
    }

    // the target finds its entry by two paths, this one and its own
    internal static void CheckOfferedOnce(String path, DeclarationMetadata declaration)
    {
        var twice = declaration.BasedOn.GroupBy(b => ParsePath(b.Target)).FirstOrDefault(g => g.Count() > 1);
        if (twice != null)
            throw new InvalidOperationException($"basedOn: {path}: {twice.First().Target} is offered twice");
    }

    // the rule '?Op=' follows: the name is the target's choice to offer, and a target with a list needs one
    internal static void CheckBasedOnOperation(String head, BasedOnMetadata b, NormalEndpointMetadata target)
    {
        var names = target.Declaration.Operations;
        if (names.Count == 0)
        {
            if (b.Operation != null)
                throw new InvalidOperationException($"{head}: names the operation '{b.Operation}', and {target.Path} lists none. Drop 'operation'.");
            return;
        }
        if (b.Operation == null)
            throw new InvalidOperationException($"{head}: {target.Path} lists operations - name one in 'operation': {String.Join(", ", names)}");
        if (!names.Contains(b.Operation))
            throw new InvalidOperationException($"{head}: '{b.Operation}' is not an operation of {target.Path}. Its operations: {String.Join(", ", names)}");
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
            TagEndpointMetadata or AdminEndpointMetadata => null,
            _ => throw new InvalidOperationException($"Unknown endpoint {endpoint.Path}")
        };
        if (meta == null)
            return;

        static IEnumerable<TableColumn> GetAllReferences(TableMetadata table)
        {
            // the baseline too: its columns are materialized (TableMetadata.Construct) and keep RefTable
            return table.AllColumns(c => c.IsRef)
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
                else if (gcol.Type == ColumnType.Folder)
                {
                    // no address of their own: the rows are the owner's $Folders, picked at the owner's address
                    gcol.RefTable = new FoldersTarget(TableMetadataDefaults.CreateFoldersTable(meta), endpoint.Path);
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
            {
                // a type that names what it points at has nothing to point at without one
                if (group.FirstOrDefault(c => TargetOf(c.Type) != null) is { } untargeted)
                    throw new InvalidOperationException(
                        $"{endpoint.Path}: [{untargeted.Name}] is '{untargeted.Type}' and declares no 'target' - it has to name {TargetOf(untargeted.Type)!.Value.What}");
                continue;
            }

            var (schema, table) = ParsePath(column.Target);
            var refMeta = await GetNormalEndpointAsync(load, dataSource, schema, table);
            foreach (var gcol in group)
            {
                CheckTargetKind(endpoint, gcol, refMeta.Storage);
                gcol.RefTable = refMeta;
            }
        }

        CheckLiteralInitials(endpoint, meta);

        if (endpoint is NormalEndpointMetadata { Storage.Kind: TableKind.Ledger } ledger)
            CheckSplitColumns(ledger.Storage);

        await CheckAutonumDeclaredAsync(load, endpoint, dataSource);

        await ResolvePostAsync(load, endpoint, dataSource);

        await ResolveBasedOnAsync(load, endpoint, dataSource);
    }

    /* 'autonum' names a row of another endpoint's file, so it can only be checked here - phase 2,
     * through the same load, the way a reference target is resolved. Deferring it was a mistake
     * paid for once already: the failure surfaced at the first save of the first document, from
     * inside a procedure, which is as far from the file as it gets.
     *
     * The numbering is not stored on the endpoint afterwards. It is a key the save writes into a
     * call and nothing else reads, so keeping the resolved row would put a second copy of what the
     * file says next to the file's own word.
     */
    private async Task CheckAutonumDeclaredAsync(EndpointLoad load, EndpointMetadata endpoint, String? dataSource)
    {
        if (endpoint is not NormalEndpointMetadata normal)
            return;
        var autonum = normal.Declaration.Autonum;
        if (String.IsNullOrEmpty(autonum))
            return;

        var registry = (await GetNormalEndpointAsync(load, dataSource,
            Constants.SchemaNames.Autonum, String.Empty)).Storage;
        if (registry.Autonums.FirstOrDefault(a => a.Id == autonum) is { } numbering)
        {
            CheckPrefix(normal, numbering);
            return;
        }

        var declared = registry.Autonums.Count == 0
            ? "it declares none"
            : $"declared: {String.Join(", ", registry.Autonums.Select(a => $"'{a.Id}'"))}";
        throw new InvalidOperationException(
            $"{endpoint.Path}: 'autonum' names '{autonum}', which {MetadataFileName(Constants.SchemaNames.Autonum, String.Empty)} does not declare - {declared}");
    }

    /* '{p}' is read at the far end of a chain - the company column of this table, the catalog it
     * points at, the prefix column there - and the procedure substitutes whatever arrives, so a
     * missing link would issue numbers with the prefix silently gone. Asked per endpoint and not per
     * numbering: documents share one, and only some of them may have a company. Here, because the
     * company's target is linked just above.
     */
    internal static void CheckPrefix(NormalEndpointMetadata endpoint, AutonumMetadata numbering)
    {
        if (!numbering.Pattern.Contains("{p}"))
            return;
        var head = $"{endpoint.Path}: numbering '{numbering.Id}' writes {{p}}";
        var company = endpoint.Storage.AllColumns().FirstOrDefault(c => c.Type == ColumnType.Company)
            ?? throw new InvalidOperationException(
                $"{head}, the company's prefix, and {endpoint.Storage.Path} declares no column of type 'company'");
        var catalog = company.RefTableCheck.Storage;
        var prefixes = catalog.AllColumns(c => c.Type == ColumnType.Prefix).ToList();
        if (prefixes.Count != 1)
            throw new InvalidOperationException(prefixes.Count == 0
                ? $"{head}, and {catalog.Path}, the catalog [{company.Name}] points at, declares no column of type 'prefix'"
                : $"{head}, and {catalog.Path} declares {String.Join(", ", prefixes.Select(c => $"[{c.Name}]"))} of type 'prefix' - which one is the prefix would be a guess");
    }

    /* Five column types name WHAT they point at and not merely that they point: an account is a
     * code of a chart, a value a code of a set, a state a code of a set that carries a life cycle,
     * a company a row of a catalog, a basis a document. The first three are spelled as their target's
     * key (ToSqlDbTypeInfo), so a target of another kind is a foreign key that cannot hold - platformid
     * against nvarchar, discovered at deploy. The last two would hold: '{p}' would then read a prefix
     * off a document, and a document would be born from a catalog row.
     *
     * One table rather than an 'if' per type: until now only the account was asked, and an enum
     * column pointing at a catalog went all the way to the database before saying anything. The
     * two facts travel together because the message needs both - the kind to compare and the
     * address to suggest.
     */
    private static (TableKind Kind, String What)? TargetOf(ColumnType type) => type switch
    {
        ColumnType.Account => (TableKind.AccPlan, $"a chart of accounts (/{Constants.SchemaNames.AccPlan}/<name>)"),
        ColumnType.Enum => (TableKind.Enum, $"a set of values (/{Constants.SchemaNames.Enum}/<name>)"),
        ColumnType.State => (TableKind.State, $"a set of states (/{Constants.SchemaNames.State}/<name>)"),
        ColumnType.Company => (TableKind.Catalog, $"a catalog (/{Constants.SchemaNames.Catalog}/<name>)"),
        ColumnType.BasedOn => (TableKind.Document, $"a document (/{Constants.SchemaNames.Document}/<name>)"),
        _ => null
    };

    /* SplitBy is written in the chart and names columns of the ledger, so it is checked where the two
     * meet - here, the chart linked just above. Every ledger over the chart must have them: one chart
     * says one thing about 361 to all of its ledgers. An analytic of the author, never the provenance
     * (the posting document would split every invoice from its payment) nor a measure.
     */
    internal static void CheckSplitColumns(TableMetadata ledger)
    {
        var chart = ledger.AllColumns().First(c => c.Name == Constants.FieldNames.Acc).RefTableCheck.Storage;
        foreach (var row in chart.Rows)
            foreach (var name in row.SplitBy)
            {
                var column = ledger.Columns.FirstOrDefault(c => c.Name == name);
                if (column == null || column.IsProvenance || column.IsAdditive)
                    throw new InvalidOperationException(
                        $"{ledger.Path}: account '{row.Id}' of {chart.Path} is split by [{name}], which {(column == null
                            ? "is not a column of the ledger" : column.IsProvenance ? "the platform fills" : "is a measure")} - SplitBy names an analytic the ledger declares");
            }
    }

    internal static void CheckTargetKind(EndpointMetadata endpoint, TableColumn column, TableMetadata target)
    {
        if (TargetOf(column.Type) is not { } required || target.Kind == required.Kind)
            return;
        throw new InvalidOperationException(
            $"{endpoint.Path}: [{column.Name}] is '{column.Type}', so '{column.Target}' has to name {required.What} - and {target.Path} is a {target.Kind}");
    }

    /* A literal initial value on a set column names a code, and here - and only here - is the
     * first moment the codes are known: the set is the far half of a reference, linked just above.
     * Without this the typo is silent in the worst way: '@map' finds no row, the RefId resolves to
     * nothing, and a NEW card simply opens with an empty control.
     *
     * Only sets are checked, because only they declare their rows. A literal pointing at a catalog
     * names an identifier that exists in the database and not in any file.
     */
    private static void CheckLiteralInitials(EndpointMetadata endpoint, TableMetadata meta)
    {
        if (endpoint is not NormalEndpointMetadata normal)
            return;
        foreach (var (key, initial) in normal.Declaration.Initials)
        {
            if (initial.Source != InitialSource.Literal)
                continue;
            var column = meta.AllColumns().FirstOrDefault(c => c.Name == key && c.IsSetRef);
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

    /* A build copies the declarations next to its output, and the walk would read 'bin/Debug' as an
     * endpoint address. Only the first segment of the module is asked: 'obj' deeper down is a table.
     */
    private static Boolean IsBuildOutput(String file) =>
        file.NormalizeSlash().Split('/').SkipWhile(s => s.StartsWith('$')).FirstOrDefault() is "bin" or "obj";

    internal async Task<(IEnumerable<TableMetadata> Tables, IReadOnlyList<(String Endpoint, EndpointGrant Grant)> Grants)>
        AllElementsMetadata(String? dataSource)
    {
        var allMeta = _codeProvider.EnumerateAllFilesRecursive("", "metadata.json");
        var tables = new List<TableMetadata>();
        var operations = new List<OperationMetadata>();
        var grants = new List<(String Endpoint, EndpointGrant Grant)>();
        foreach (var file in allMeta.Where(f => !IsBuildOutput(f)))
        {
            var endpointPath = Path.GetDirectoryName(file)?.NormalizeSlash();
            if (endpointPath == null)
                continue;
            var (schema, table) = ParsePath(endpointPath);
            // rights are held on an address, a report's and a document's over a storage too - so before both filters
            var loaded = await GetEndpointAsync(dataSource, schema, table);
            grants.AddRange((loaded.Grants ?? []).Select(g => (loaded.Path, g)));
            /* Only a data endpoint declares a table, and only one that does not point elsewhere:
             * a shared storage is deployed by the file that declares it, a report declares none.
             * Every operation of a document is a row of the registry - the ones it lists, whatever
             * table it lives in, and the implicit one of a document over a shared storage.
             */
            if (loaded is not NormalEndpointMetadata endpoint)
                continue;
            operations.AddRange(endpoint.DocumentOperations()
                .Select((id, ix) => new OperationMetadata(id, endpoint.Name, endpoint.Path, ix + 1)));
            if (!endpoint.Declaration.HasOwnShape)
                continue;
            tables.Add(endpoint.Storage);
        }
        /* The registry is a table like a set: deployed with its rows (OperationRows), and the rows
         * reach the hash through Xtra.
         *
         * An operation's Id starts with the last segment of its document's address, so two folders
         * of one kind (an alias, app.json) can give two documents one name and their operations one
         * Id. Merged, they would be one row, and both registers would filter by it and show each
         * other's documents - so it is refused here, where both addresses are still known.
         */
        var twice = operations.GroupBy(o => o.Id).FirstOrDefault(g => g.Count() > 1);
        if (twice != null)
            throw new InvalidOperationException($"""
                {String.Join(" and ", twice.Select(o => o.Path))} give one operation '{twice.Key}': an operation's Id is the name of its document's folder (and '.<operation>'), and the folders around it do not count.
                  Rename one of them.
                """);
        if (operations.Count > 0)
            tables.Add(TableMetadataDefaults.OperationsTable() with { Rows = OperationRows.Rows(operations) });
        return (tables, grants);
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

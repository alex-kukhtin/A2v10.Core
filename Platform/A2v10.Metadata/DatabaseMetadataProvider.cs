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

// the one key of app.json the generator reads; the rest of the file belongs to the client
internal sealed record AppJsonPlatformId
{
    public String? PlatformId { get; set; }
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

    public Task<UIElement> GetXamlFormAsync(String? dataSource, EndpointMetadata endpoint, String key, Func<UIElement> defForm)
    {
        return _metadataCache.GetOrAddXamlFormAsync(dataSource, endpoint, key, defForm);
    }

    /* The database is the fact once it holds the type. Before the first deploy it holds nothing,
     * and then - only then - app.json says what the deploy creates it from. No default and no
     * fallback: guessing a base would not surface as a failure, it would write values of the wrong
     * shape into a live database. So absent in both is an error, and a declaration disagreeing
     * with the database is one too - the base never changes.
     */
    private async Task<AppPlatformId> LoadPlatformIdAsync(String? dataSource)
    {
        var found = await _dbContext.LoadAsync<PlatformIdType>(dataSource, "a2meta.[GetPlatformIdType]");
        var declared = await DeclaredPlatformIdAsync();
        if (found?.DataType is String fact)
        {
            var actual = AppPlatformId.FromSqlName(fact);
            if (declared != null && declared != actual)
                throw new InvalidOperationException(
                    $"app.json: 'platformid' is '{declared.SqlTypeName}', but the database rests on '{fact}'. The base never changes.");
            return actual;
        }
        return declared
            ?? throw new InvalidOperationException(
                "The 'platformid' type is not defined in the database, and app.json declares no 'platformid' to create it from");
    }

    // internal for EndpointValidator: the declaration is the only answer a check that never reads the database can have
    internal async Task<AppPlatformId?> DeclaredPlatformIdAsync()
    {
        using var stream = _codeProvider.FileStreamRO("app.json", primaryOnly: true);
        if (stream == null)
            return null;
        using var sr = new StreamReader(stream);
        var app = JsonConvert.DeserializeObject<AppJsonPlatformId>(await sr.ReadToEndAsync());
        return app?.PlatformId is String name ? AppPlatformId.FromSqlName(name) : null;
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
    private async Task<TableMetadata> BuildStorageAsync(String schema, String table, String text, String? hash)
    {
        var storage = JsonConvert.DeserializeObject<TableMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
            ?? throw new InvalidOperationException($"{MetadataFileName(schema, table)}: TableMetadata deserialization fails");
        storage.FileHash = hash;
        storage.SetDefaults(schema, table);
        CheckNames(storage, schema, table);
        CheckValues(storage, schema, table);
        CheckAutonums(storage, schema, table);
        CheckAccPlan(storage, schema, table);
        await LoadSeedAsync(storage, schema, table);
        return storage;
    }

    /* A ledger posts against one chart, and nothing names it but this key - no default, and not the
     * ledger's folder name: ledger/national -> accplan/national is a coincidence, not a rule. Anywhere
     * else the key is refused rather than dropped. That the path is a chart is asked in phase 2, where
     * the target is linked (ResolveReferencesAsync).
     */
    private static void CheckAccPlan(TableMetadata storage, String schema, String table)
    {
        var file = MetadataFileName(schema, table);
        if (storage.Kind != EndpointKind.Ledger)
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

    /* 'seed' is a key of every table and is processed per kind; today only a chart of accounts has
     * the processing, so anywhere else it is refused rather than dropped. On a chart it is required:
     * nothing is inferred from a file lying in the folder.
     *
     * Everything about the rows is checked here, without a database, so a wrong file fails the load
     * and never the deploy: keys against the columns, values against their columns' literal, the
     * closed sets, the tree. The deploy then writes what was checked (SqlDbGenerator.CreateSeedScript).
     */
    private async Task LoadSeedAsync(TableMetadata storage, String schema, String table)
    {
        var file = MetadataFileName(schema, table);
        if (storage.Kind != EndpointKind.AccPlan)
        {
            if (!String.IsNullOrEmpty(storage.Seed))
                throw new InvalidOperationException(
                    $"{file}: 'seed' is read for {Constants.SchemaNames.AccPlan}/ only; for {schema}/ it is not processed yet");
            return;
        }
        if (String.IsNullOrEmpty(storage.Seed))
            throw new InvalidOperationException(
                $"{file}: does not declare 'seed'. A chart of accounts is its seed file: \"seed\": \"seed.json\"");

        var seedFile = Path.Combine(schema, table, storage.Seed).NormalizeSlash();
        using var stream = _codeProvider.FileStreamRO(seedFile)
            ?? throw new InvalidOperationException($"{file}: 'seed' names {seedFile}, which does not exist");
        using var sr = new StreamReader(stream);
        var json = JsonConvert.DeserializeObject<Dictionary<String, Dictionary<String, JToken>>>(await sr.ReadToEndAsync())
            ?? throw new InvalidOperationException($"{seedFile}: expected an object - account code: {{ column: value }}");

        storage.SeedRows = [.. json.Select(kv => AccountRow(seedFile, storage, kv.Key, kv.Value))
            .OrderBy(r => r.Id, StringComparer.Ordinal)];
        CheckAccountTree(seedFile, storage.SeedRows);
    }

    // what a seed row names: the columns the platform reads, and the author's own
    private static readonly String[] _accountColumns = [Constants.FieldNames.Name, Constants.FieldNames.Parent,
        Constants.FieldNames.AccountType, Constants.FieldNames.NormalBalance];
    private static readonly String[] _accountRequired = [Constants.FieldNames.Name,
        Constants.FieldNames.AccountType, Constants.FieldNames.NormalBalance];

    private static SeedRow AccountRow(String seedFile, TableMetadata storage, String id, Dictionary<String, JToken> row)
    {
        var head = $"{seedFile}: account '{id}'";
        if (String.IsNullOrEmpty(id))
            throw new InvalidOperationException($"{seedFile}: an account under an empty code");
        /* '.' separates what the user adds under an account of the file (361.1) - the same figure as
         * '$' in table names. Refused here, a release cannot declare a code a user may already hold.
         */
        if (id.Contains('.'))
            throw new InvalidOperationException(
                $"{head} - '.' is not allowed in a code of the file; it separates the sub-accounts a user adds (361.1)");
        var keyLength = storage.KeyColumn.DeployLength();
        if (id.Length > keyLength)
            throw new InvalidOperationException($"{head} - a code is at most {keyLength} characters");

        var values = new Dictionary<String, String?>();
        foreach (var (name, token) in row)
        {
            var column = storage.AllColumns().FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException(
                    $"{head} - '{name}' is not a column of {storage.SqlTableName}");
            if (!_accountColumns.Contains(name) && !storage.Columns.Any(c => c.Name == name))
                throw new InvalidOperationException(
                    $"{head} - '{name}' is written by the platform, not by the seed");
            var value = token.Type switch
            {
                JTokenType.Null => null,
                JTokenType.String or JTokenType.Integer or JTokenType.Float or JTokenType.Boolean
                    => ((JValue)token).ToString(CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException($"{head} - '{name}' is not a scalar value")
            };
            if (value != null)
            {
                try
                {
                    column.SqlLiteral(value);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"{head} - {ex.Message}", ex);
                }
            }
            values.Add(name, value);
        }

        foreach (var name in _accountRequired)
            if (values.GetValueOrDefault(name) == null)
                throw new InvalidOperationException($"{head} - '{name}' is required");
        CheckClosedSet<AccountType>(head, values, Constants.FieldNames.AccountType);
        CheckClosedSet<NormalBalance>(head, values, Constants.FieldNames.NormalBalance);
        return new SeedRow(id, values);
    }

    // spelled exactly as the enum member: the value is stored by name and compared by name
    private static void CheckClosedSet<T>(String head, Dictionary<String, String?> values, String name) where T : struct, Enum
    {
        var value = values[name]!;
        if (!Enum.GetNames<T>().Contains(value))
            throw new InvalidOperationException(
                $"{head} - {name} '{value}' is not one of {String.Join(", ", Enum.GetNames<T>())}");
    }

    // a Parent is a code of the same file, and following Parents never comes back
    private static void CheckAccountTree(String seedFile, List<SeedRow> rows)
    {
        var parents = rows.ToDictionary(r => r.Id, r => r.Values.GetValueOrDefault(Constants.FieldNames.Parent));
        foreach (var (id, parent) in parents)
        {
            if (parent != null && !parents.ContainsKey(parent))
                throw new InvalidOperationException(
                    $"{seedFile}: account '{id}' - Parent '{parent}' is not an account of this file");
            var seen = new HashSet<String> { id };
            for (var p = parent; p != null; p = parents[p])
                if (!seen.Add(p))
                    throw new InvalidOperationException(
                        $"{seedFile}: account '{id}' - the Parent chain comes back to '{p}'");
        }
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

    /* The rows of a set, checked where the file is read and without a database - keys, colours and
     * roles - so a wrong file fails the load and never the deploy. Asked of EVERY file and not only
     * of a set: 'values' written where nothing reads them was silently dropped, which is the one
     * failure of this format that leaves no trace at all.
     *
     * The two halves refuse each other's keys. A colour and a role on an enum have no column to
     * land in; a role missing on a state leaves the cycle without the three facts it is read for.
     * A key with no effect is worse than a missing one - it teaches the next reader that it has one.
     */
    private static void CheckValues(TableMetadata storage, String schema, String table)
    {
        var file = MetadataFileName(schema, table);

        /* The same key one floor down. A collection is a TableMetadata too and is deserialized from
         * the same text, so 'values' written inside 'details' arrives in a node nothing walks - the
         * identical silence, closed here rather than left to the schema, which is read by an editor
         * and not by the loader.
         */
        foreach (var (key, detail) in storage.Details)
            if (detail.Values.Count > 0)
                throw new InvalidOperationException(
                    $"{file}: 'values' inside details '{key}'. Rows of a collection are data; a closed list a column points at is a set of its own, declared in {Constants.SchemaNames.Enum}/<name> or {Constants.SchemaNames.State}/<name>");

        if (!storage.IsSet)
        {
            if (storage.Values.Count > 0)
                throw new InvalidOperationException($"""
                    {file}: declares 'values', which are the rows of a SET, and {schema}/ is not one.
                      A closed list a column points at is declared in {Constants.SchemaNames.Enum}/<name> (a code and a name)
                      or in {Constants.SchemaNames.State}/<name> (a life cycle: a colour and a role as well).
                    """);
            return;
        }

        var keyLength = storage.KeyColumn.DeployLength();
        foreach (var value in storage.Values)
        {
            if (String.IsNullOrEmpty(value.Id))
                throw new InvalidOperationException($"{file}: a value with no 'id'. The id is the code the referencing column stores");
            if (value.Id.Length > keyLength)
                throw new InvalidOperationException($"{file}: value '{value.Id}' - a code is at most {keyLength} characters");
        }

        /* Case-insensitively, because that is how the database will compare them: the merge matches
         * on the key under the server's collation, and two codes differing only in case meet there
         * as one row - 'attempted to UPDATE or DELETE the same row more than once', at deploy time,
         * naming neither the file nor the value.
         */
        var twice = storage.Values.GroupBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (twice != null)
            throw new InvalidOperationException(
                $"{file}: '{twice.Key}' is declared {twice.Count()} times. A code names one value of the set");

        if (!storage.IsState)
        {
            var extra = storage.Values.FirstOrDefault(v => v.Color != null || v.Role != null);
            if (extra != null)
                throw new InvalidOperationException($"""
                    {file}: value '{extra.Id}' declares '{(extra.Color != null ? "color" : "role")}', which belongs to a set of states.
                      An enum value is a code and a name; a colour and a role are read from {Constants.SchemaNames.State}/<name>.
                    """);
            return;
        }

        foreach (var value in storage.Values)
        {
            if (value.Role == null)
                throw new InvalidOperationException($"""
                    {file}: state '{value.Id}' declares no 'role', so nothing says what it is to the cycle.
                      One of: {String.Join(", ", Enum.GetNames<StateRole>())}.
                    """);
            CheckColor(file, value);
        }

        /* Over the living values alone. A void state is withdrawn - it keeps the records that
         * already carry it and leaves the candidates - so a new record cannot start on it and a
         * cycle cannot end there. A void Initial beside a live one is the legitimate shape of a
         * migration, and counting it would refuse exactly that.
         */
        void Exactly(StateRole role, String what)
        {
            var found = storage.Values.Where(v => !v.Void && v.Role == role).ToList();
            if (found.Count == 1)
                return;
            throw new InvalidOperationException(found.Count == 0
                ? $"{file}: no state is '{role}', and {what}"
                : $"{file}: {String.Join(" and ", found.Select(v => $"'{v.Id}'"))} are all '{role}', and {what}");
        }

        Exactly(StateRole.Initial, "a new record has to start on one definite state");
        Exactly(StateRole.Success, "success is measured, so one state IS reaching the goal - a cycle with two good endings picks one and tells the rest apart by a field of its own");
    }

    /* Against the styles the view engine draws, which is the list the CSS agrees with - so a colour
     * the load accepts is a colour that renders. Not the picker's list (main.js): that one answers
     * 'what to offer', a shorter question, and would refuse names that draw perfectly well.
     *
     * Lower case exactly, not case-insensitively: the value is written into a class attribute and
     * CSS class names are case-sensitive, so 'Green' would pass a lenient check and then render a
     * badge with no colour and no error anywhere.
     */
    private static void CheckColor(String file, SetValueMetadata value)
    {
        if (value.Color == null)
            return;
        if (Enum.GetNames<TagLabelStyle>().Any(n => n.ToLowerInvariant() == value.Color))
            return;
        var known = Enum.GetNames<TagLabelStyle>().Select(n => n.ToLowerInvariant());
        throw new InvalidOperationException($"""
            {file}: state '{value.Id}' - '{value.Color}' is not a colour. They are written in lower case, and they are:
              {String.Join(", ", known)}.
            """);
    }

    /* Refused where the file is read, because nothing downstream can report it: the SQL that issues
     * a number cannot parse a pattern it was handed - a pattern with no counter in it yields a
     * number, the same one for every document, and it gets saved.
     *
     * Asked of every file and not only of /autonum, so 'autonums' written somewhere it means
     * nothing is an error rather than a key silently dropped on the way to the deploy.
     */
    private static void CheckAutonums(TableMetadata storage, String schema, String table)
    {
        if (storage.Autonums.Count == 0)
            return;
        var file = MetadataFileName(schema, table);
        if (storage.Kind != EndpointKind.Autonum)
            throw new InvalidOperationException(
                $"{file}: 'autonums' declares numberings, and they are declared in autonum/metadata.json. Name one here with \"autonum\": \"<id>\" instead");

        foreach (var autonum in storage.Autonums)
        {
            if (String.IsNullOrEmpty(autonum.Id))
                throw new InvalidOperationException($"{file}: 'autonums' declares a numbering under an empty key");
            if (String.IsNullOrEmpty(autonum.Pattern))
                throw new InvalidOperationException(
                    $"{file}: '{autonum.Id}' declares no 'pattern'. The pattern IS the number: \"{{yyyy}}-{{nnnnn}}\"");
            CheckPattern(file, autonum);
        }
    }

    // The date placeholders the procedure substitutes. '{n}' through '{nnnnn}' is the counter and
    // is checked apart: its length is what it says, so it is not a name in a list.
    private static readonly String[] _autonumPlaceholders = ["yy", "yyyy", "mm", "qq"];

    /* Every '{...}' is read, because the procedure does not read them - it substitutes the ones it
     * knows and leaves the rest standing in the number. And the counter is checked twice: that it
     * is there at all, and that the pattern tells its periods apart. A monthly counter under a
     * pattern with no month issues '5' twice a year, which is the one failure here that produces a
     * plausible document rather than an error.
     */
    private static void CheckPattern(String file, AutonumMetadata autonum)
    {
        var pattern = autonum.Pattern;
        var head = $"{file}: pattern '{pattern}' of '{autonum.Id}'";
        var found = new List<String>();
        var counter = false;
        var ix = 0;
        while (ix < pattern.Length)
        {
            var start = pattern.IndexOf('{', ix);
            if (start < 0)
                break;
            var end = pattern.IndexOf('}', start);
            if (end < 0)
                throw new InvalidOperationException($"{head} has a '{{' that is never closed");
            var token = pattern[(start + 1)..end];
            if (token.Length > 0 && token.All(c => c == 'n'))
            {
                if (counter)
                    throw new InvalidOperationException(
                        $"{head} writes the counter twice; only the first would be filled in");
                counter = true;
            }
            else if (!_autonumPlaceholders.Contains(token))
                throw new InvalidOperationException(
                    $"{head} uses '{{{token}}}', which is nothing. Known: {{yy}}, {{yyyy}}, {{mm}}, {{qq}}, and {{n}} to {{nnnnn}} for the counter");
            found.Add(token);
            ix = end + 1;
        }
        if (!counter)
            throw new InvalidOperationException(
                $"{head} has no counter in it. Write it as '{{n}}', one 'n' per digit - '{{nnnnn}}' pads to five");

        var year = found.Contains("yy") || found.Contains("yyyy");
        var missing = autonum.Period switch
        {
            AutonumPeriod.Year => year ? null : "{yyyy}",
            AutonumPeriod.Quarter => !year ? "{yyyy}" : found.Contains("qq") ? null : "{qq}",
            AutonumPeriod.Month => !year ? "{yyyy}" : found.Contains("mm") ? null : "{mm}",
            _ => null
        };
        if (missing != null)
            throw new InvalidOperationException(
                $"{head} restarts every {autonum.Period.ToString().ToLowerInvariant()}, so it has to carry {missing} - without it one number is issued in two periods");
    }

    private async Task<TableMetadata> LoadStorageAsync(String? dataSource, String schema, String table)
    {
        var (text, hash) = await ReadMetadataFileAsync(schema, table);
        return await BuildStorageAsync(schema, table, text, hash);
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
                (_, s, t) => BuildStorageAsync(s, t, text, hash));
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
                    Declaration = BakeDeclaration(MergeDeclaration(declaration, storageDeclaration), storage, schema, table,
                        await GetPlatformIdAsync(dataSource)),
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
            Constants.SchemaNames.State => EndpointKind.State,
            Constants.SchemaNames.AccPlan => EndpointKind.AccPlan,
            Constants.SchemaNames.Ledger => EndpointKind.Ledger,
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
     * write to), 'surface' (a shape I only read) - and which are legal is decided per folder:
     * 'table' or 'storage' for the kinds that render (document, catalog, journal - an operation,
     * or a second address with screens of its own over the same rows), 'surface' for a report,
     * which owns no table and therefore never reaches deploy, 'table' alone for a set. Writing the
     * wrong key never moves an endpoint into another rule - it is an error naming the rule it
     * broke.
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

        /* The kinds that render: a second endpoint over one table is a second window on the same
         * rows - an operation of a document, a catalog or a journal with its own screens and its own
         * address. A set has no screen, so a second address to it would be an address to nothing.
         */
        if (schema is not (Constants.SchemaNames.Document or Constants.SchemaNames.Catalog or Constants.SchemaNames.Journal))
        {
            if (hasStorage)
                throw new InvalidOperationException($"""
                    {file}: declares 'storage', which '{schema}/' may not do.
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
                        "storage": "/{schema}/<name>" - if it is a second one over a table declared elsewhere (an operation, a second screen).
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
        if (normal.Declaration.Post is not { Count: > 0 } post)
            return;

        async Task<TableMetadata> JournalAsync(String path)
        {
            var (schema, table) = ParsePath(path);
            return (await GetNormalEndpointAsync(load, dataSource, schema, table)).Storage;
        }

        /* The key names the kind of its target, so a path of the other kind is refused here - past this
         * point a ledger under 'journal' fails as a column nobody can resolve, far from the cause.
         * 'journals' of a procedure do not take a ledger yet.
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
                    if (!chart.SeedRows.Any(r => r.Id == code))
                        throw new InvalidOperationException(
                            $"post: {normal.Path} -> {ledger.Path}: '{leg}' const [{name}] = '{code}' is not an account of {chart.Path}");
                }
        }

        async Task<TableMetadata> TargetAsync(String key, String path, EndpointKind kind)
        {
            var target = await JournalAsync(path);
            if (target.Kind != kind)
                throw new InvalidOperationException(
                    $"post: {normal.Path}: '{key}' names {path}, which is a {target.Kind}, not a {kind}");
            return target;
        }

        foreach (var p in post)
        {
            if (!p.IsSql)
            {
                p.TargetTable = p.IsLedger
                    ? await TargetAsync("ledger", p.Ledger!, EndpointKind.Ledger)
                    : await TargetAsync("journal", p.Journal!, EndpointKind.Journal);
                if (p.IsLedger)
                    await CheckConstAccountsAsync(p);
                continue;
            }
            // assigned, never appended: a second pass would otherwise double the list
            var journals = new List<TableMetadata>();
            foreach (var path in p.Journals)
                journals.Add(await TargetAsync("journals", path, EndpointKind.Journal));
            p.SqlTargets = journals;
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
            {
                CheckTargetKind(endpoint, gcol, refMeta.Storage);
                gcol.RefTable = refMeta;
            }
        }

        CheckLiteralInitials(endpoint, meta);

        await CheckAutonumDeclaredAsync(load, endpoint, dataSource);

        await ResolvePostAsync(load, endpoint, dataSource);
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
        if (registry.Autonums.Any(a => a.Id == autonum))
            return;

        var declared = registry.Autonums.Count == 0
            ? "it declares none"
            : $"declared: {String.Join(", ", registry.Autonums.Select(a => $"'{a.Id}'"))}";
        throw new InvalidOperationException(
            $"{endpoint.Path}: 'autonum' names '{autonum}', which {MetadataFileName(Constants.SchemaNames.Autonum, String.Empty)} does not declare - {declared}");
    }

    /* Three column types name WHAT they point at and not merely that they point: an account is a
     * code of a chart, a value a code of a set, a state a code of a set that carries a life cycle.
     * Each is spelled as its target's key (ToSqlDbTypeInfo), so a target of another kind is a
     * foreign key that cannot hold - platformid against nvarchar, discovered at deploy.
     *
     * One table rather than an 'if' per type: until now only the account was asked, and an enum
     * column pointing at a catalog went all the way to the database before saying anything. The
     * two facts travel together because the message needs both - the kind to compare and the
     * address to suggest.
     */
    private static (EndpointKind Kind, String What)? TargetOf(ColumnType type) => type switch
    {
        ColumnType.Account => (EndpointKind.AccPlan, $"a chart of accounts (/{Constants.SchemaNames.AccPlan}/<name>)"),
        ColumnType.Enum => (EndpointKind.Enum, $"a set of values (/{Constants.SchemaNames.Enum}/<name>)"),
        ColumnType.State => (EndpointKind.State, $"a set of states (/{Constants.SchemaNames.State}/<name>)"),
        _ => null
    };

    private static void CheckTargetKind(EndpointMetadata endpoint, TableColumn column, TableMetadata target)
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

    private async Task<IEnumerable<TableMetadata>> AllElementsMetadata(String? dataSource)
    {
        var allMeta = _codeProvider.EnumerateAllFilesRecursive("", "metadata.json");
        var tables = new List<TableMetadata>();
        var operations = new List<OperationMetadata>();
        foreach (var file in allMeta.Where(f => !IsBuildOutput(f)))
        {
            var endpointPath = Path.GetDirectoryName(file)?.NormalizeSlash();
            if (endpointPath == null)
                continue;
            var (schema, table) = ParsePath(endpointPath);
            /* Only a data endpoint declares a table, and only one that does not point elsewhere:
             * a shared storage is deployed by the file that declares it, a report declares none.
             * One that points at a document storage is an operation of it - a row of the registry.
             */
            if (await GetEndpointAsync(dataSource, schema, table) is not NormalEndpointMetadata endpoint)
                continue;
            if (!endpoint.Declaration.HasOwnShape)
            {
                if (endpoint.DocumentOperation() is { Length: > 0 } op)
                    operations.Add(new OperationMetadata(op));
                continue;
            }
            tables.Add(endpoint.Storage);
        }
        /* The registry is a table like a set: deployed with its rows, and the rows reach the
         * hash through Xtra. Sorted, because the fingerprint is taken from the text and must not
         * depend on how the file system is enumerated.
         */
        if (operations.Count > 0)
            tables.Add(TableMetadataDefaults.OperationsTable() with
            {
                Operations = [.. operations.DistinctBy(o => o.Id).OrderBy(o => o.Id, StringComparer.Ordinal)]
            });
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

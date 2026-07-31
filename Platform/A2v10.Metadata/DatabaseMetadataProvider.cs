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

    public Task<EndpointMetadata> GetEndpointAsync(String? dataSource, String schema, String table)
    {
        return _metadataCache.GetOrAddAsync(dataSource, schema, table, LoadEndpointAsync);
    }

    public async Task<TableMetadata> GetSchemaAsync(IModelBaseMeta meta, String? dataSource)
    {
        var endpoint = await GetEndpointAsync(dataSource, meta.CurrentSchema, meta.CurrentTable);
        var loaded = endpoint.Table;
        if (!ReferenceEquals(endpoint.Table, endpoint.Declaration))
            loaded.Origin = endpoint.Declaration;
        await ResolveReferencesAsync(loaded, dataSource);
        await ResolvePostedAsync(endpoint.Declaration, dataSource);
        loaded.SetDefaults(meta.CurrentSchema, meta.CurrentTable);
        return loaded;
    }
    public async Task<TableMetadata> GetSchemaAsync(String? dataSource, String schema, String table)
    {
        var endpoint = await GetEndpointAsync(dataSource, schema, table);
        var loaded = endpoint.Table;
        if (!ReferenceEquals(endpoint.Table, endpoint.Declaration))
            loaded.Origin = endpoint.Declaration;
        await ResolveReferencesAsync(loaded, dataSource);
        loaded.SetDefaults(schema, table);
        return loaded;
    }

    String? GetDefaultStorage(TableMetadata table, String schema)
    {
        if (String.IsNullOrEmpty(table.Storage) && schema == Constants.SchemaNames.Document)
            return Constants.SchemaNames.Document;
        return table.Storage;
    }

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
    public Task<UIElement> GetXamlFormAsync(String? dataSource, TableMetadata meta, String key, Func<UIElement> defForm)
    {
        return _metadataCache.GetOrAddXamlFormAsync(dataSource, meta, key, defForm);
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
    private async Task<EndpointMetadata> LoadEndpointAsync(String? dataSource, String schema, String table)
    {
        var fileName = Path.Combine(schema, table, "metadata.json");
        using var stream = _codeProvider.FileStreamRO(fileName);
        var text = "{}"; // empty value;
        String? hash = null;
        if (stream != null)
        {
            using var sr = new StreamReader(stream);
            text = await sr.ReadToEndAsync()
                ?? throw new InvalidOperationException($"{fileName} is empty");
            hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }
        var declaration = JsonConvert.DeserializeObject<TableMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
            ?? throw new InvalidOperationException("TableMetadata deserialization fails");
        declaration.FileHash = hash;

        if (!String.IsNullOrEmpty(table))
            declaration.Storage = GetDefaultStorage(declaration, schema);

        var shape = declaration;
        if (!String.IsNullOrEmpty(declaration.Storage))
        {
            var (storageSchema, storageTable) = ParsePath(declaration.Storage);
            shape = await GetSchemaAsync(dataSource, storageSchema, storageTable)
                ?? throw new InvalidOperationException($"Storage {declaration.Storage} not found");
        }

        return new EndpointMetadata()
        {
            Kind = EndpointKindOf(declaration, schema),
            Schema = schema,
            Name = table,
            Table = shape,
            Declaration = declaration,
            FileHash = hash
        };
    }

    /* Folders the enum does not know yet ('rep', 'op', 'autonum') resolve to Undefined
     * rather than throwing: the container carries the kind, nobody reads it yet, and
     * this is the single place that will learn the new kinds.
     */
    private static EndpointKind EndpointKindOf(TableMetadata declaration, String schema)
    {
        if (declaration.Kind != EndpointKind.Undefined)
            return declaration.Kind;
        return schema switch
        {
            Constants.SchemaNames.Catalog => EndpointKind.Catalog,
            Constants.SchemaNames.Document => EndpointKind.Document,
            Constants.SchemaNames.Journal => EndpointKind.Journal,
            _ => EndpointKind.Undefined
        };
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

    public async Task ResolvePostedAsync(TableMetadata meta, String? dataSource)
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

    public async Task ResolveReferencesAsync(TableMetadata meta, String? dataSource)
    {
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
                    gcol.RefTable = meta; // self!
                    continue;
                }
                else if (gcol.Type == ColumnType.Operation)
                {
                    gcol.RefTable = TableMetadataDefaults.CreateOperationsTable();
                    continue;
                }
            }

            if (column.Target == null)
                continue;

            var (schema, table) = ParsePath(column.Target);
            var refMeta = await GetSchemaAsync(dataSource, schema, table);
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
            var tableMeta = await GetSchemaAsync(dataSource, schema, table);
            if (tableMeta.Origin != null)
                continue;
            tables.Add(tableMeta);
        }
        if (tables.Any(t => t.HasTags) && !tables.Any(t => t.IsTags))
            tables.Add(TableMetadataDefaults.CreateTagsTable());
        return tables;
    }
}

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

public class DatabaseMetadataProvider(DatabaseMetadataCache _metadataCache, IDbContext _dbContext, IAppCodeProvider _codeProvider)
{
    public async Task<TableMetadata> GetSchemaAsync(IModelBaseMeta meta, String? dataSource)
    {
        var loaded = await _metadataCache.GetOrAddAsync(dataSource, meta.CurrentSchema, meta.CurrentTable, LoadTableMetadataAsync);
        if (!String.IsNullOrEmpty(meta.CurrentTable))
            loaded.Storage = GetDefaultStorage(loaded, meta.CurrentSchema);
        var storage = await ResolveStorageAsync(loaded, dataSource);
        if (storage != null)
        {
            storage.Origin = loaded;
            loaded = storage;
        }
        await ResolveReferencesAsyns(loaded, dataSource);
        loaded.SetDefaults(meta.CurrentSchema, meta.CurrentTable);
        return loaded;
    }
    public async Task<TableMetadata> GetSchemaAsync(String? dataSource, String schema, String table)
    {
        var loaded = await _metadataCache.GetOrAddAsync(dataSource, schema, table, LoadTableMetadataAsync);
        if (!String.IsNullOrEmpty(table))
            loaded.Storage = GetDefaultStorage(loaded, schema);
        var storage = await ResolveStorageAsync(loaded, dataSource);
        if (storage != null)
        {
            storage.Origin = loaded;
            loaded = storage;
        }
        await ResolveReferencesAsyns(loaded, dataSource);
        loaded.SetDefaults(schema, table);
        return loaded;
    }

    String? GetDefaultStorage(TableMetadata table, String schema)
    {
        if (String.IsNullOrEmpty(table.Storage) && schema == Constants.SchemaNames.Document)
            return Constants.SchemaNames.Document;
        return table.Storage;
    }

    public async Task<TableMetadata?> ResolveStorageAsync(TableMetadata table, String? dataSource)
    {
        if (!String.IsNullOrEmpty(table.Storage))
        {
            var (storageSchema, storageTable) = ParsePath(table.Storage);
            return await GetSchemaAsync(dataSource, storageSchema, storageTable)
                ?? throw new InvalidOperationException($"Parent Table {table.Storage} not found");
        }
        return null;
    }

    public Task<AppMetadata> GetAppMetadataAsync(String? dataSource)
    {
        return _metadataCache.GetAppMetadataAsync(dataSource, LoadAppMetadataAsync);
    }

    public async Task<EndpointTableInfo> GetModelInfoFromPathAsync(String path)
    {
        var modelTableInfo = _metadataCache.GetModelInfoFromPath(path);
        if (modelTableInfo == null) {
            var (schema, table) = ParsePath(path);
            var tableMeta = await _metadataCache.GetOrAddAsync(null, schema, table,
                LoadTableMetadataAsync);
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

    private async Task<TableMetadata> LoadTableMetadataAsync(String? dataSource, String schema, String table)
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
        var meta = JsonConvert.DeserializeObject<TableMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
            ?? throw new InvalidOperationException("TableMetadata deserialization fails");
        meta.FileHash = hash;
        return meta;
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
        var split = path.ToLowerInvariant().Split('/');
        if (split.Length == 1)
            return (split[0], String.Empty);
        if (split.Length < 2 )
            throw new InvalidOperationException($"Invalid path: {path}");
        return (split[0], split[1]);
    }

    public async Task ResolveReferencesAsyns(TableMetadata meta, String? dataSource)
    {
        IEnumerable<TableColumn> GetAllReferences(TableMetadata table)
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
            }
            var (schema, table) = ParsePath(column.Target);
            var refMeta = await GetSchemaAsync(dataSource, schema, table);
            foreach (var gcol in group)
                gcol.RefTable = refMeta;
        }
    }

}

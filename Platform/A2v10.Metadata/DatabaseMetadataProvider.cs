// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        var storage = await ResolveStorageAsync(loaded, dataSource);
        if (storage != null)
        {
            storage.StorageTopTable = loaded;
            loaded = storage;   
        }
        await ResolveReferencesAsyns(loaded, dataSource);
        loaded.SetDefaults(meta.CurrentSchema, meta.CurrentTable);
        return loaded;
    }
    public async Task<TableMetadata> GetSchemaAsync(String? dataSource, String schema, String table)
    {
        var meta = await _metadataCache.GetOrAddAsync(dataSource, schema, table, LoadTableMetadataAsync);
        var storage = await ResolveStorageAsync(meta, dataSource);
        if (storage != null)
        {
            storage.StorageTopTable = meta;
            meta = storage;
        }
        await ResolveReferencesAsyns(meta, dataSource);
        meta.SetDefaults(schema, table);
        return meta;
    }

    public async Task<TableMetadata?> ResolveStorageAsync(TableMetadata table, String? dataSource)
    {
        if (!String.IsNullOrEmpty(table.Storage))
        {
            var (storageSchema, storageTable) = DatabaseMetadataProvider.ParsePath(table.Storage);
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

    public async Task<IEnumerable<ReferenceMember>> ReferenceFieldsAsync(String? dataSource, TableMetadata table)
    {
        async Task<ReferenceMember> CreateMember(TableColumn column, Int32 index)
        {
            var targetPath = ParsePath(column.Target);
            var table = column.Type switch
            {
                ColumnType.Operation => await GetSchemaAsync(dataSource, "op", "operations"),
                _ => await GetSchemaAsync(dataSource, targetPath.schema, targetPath.table)
            };
            return new ReferenceMember(column, table, index);
        }
        Int32 index = 0;
        var list = new List<ReferenceMember>();
        foreach (var cx in table.Columns.Where(c => c.Type == ColumnType.Ref))
            list.Add(await CreateMember(cx, index++));
        return list;
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
        if (stream != null)
        {
            using var sr = new StreamReader(stream);
            text = await sr.ReadToEndAsync()
                ?? throw new InvalidOperationException($"{fileName} is empty");
        }
        var meta = JsonConvert.DeserializeObject<TableMetadata>(text, JsonSettings.CamelCaseSerializerSettings)
            ?? throw new InvalidOperationException("TableMetadata deserialization fails");
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
        if (split.Length < 2 )
            throw new InvalidOperationException($"Invalid path: {path}");
        return (split[0], split[1]);
    }

    public async Task ResolveReferencesAsyns(TableMetadata meta, String? dataSource)
    {
        foreach (var column in meta.Columns.Where(col => col.NeedLoadRef))
        {
            if (column.Type == ColumnType.Parent)
            {
                column.RefTable = meta; // self!
                continue;
            }
            var (schema, table) = DatabaseMetadataProvider.ParsePath(column.Target);
            var refMeta = await GetSchemaAsync(dataSource, schema, table);
            column.RefTable = refMeta;
        }
    }
}

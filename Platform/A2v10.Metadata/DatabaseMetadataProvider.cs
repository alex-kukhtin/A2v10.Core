// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

public class DatabaseMetadataProvider(DatabaseMetadataCache _metadataCache, IDbContext _dbContext)
{
    public Task<TableMetadata> GetSchemaAsync(IModelBaseMeta meta, String? dataSource)
    {
        return _metadataCache.GetOrAddAsync(dataSource, meta.CurrentSchema, meta.CurrentTable, LoadTableMetadataAsync);
    }
    public Task<TableMetadata> GetSchemaAsync(String? dataSource, String schema, String table)
    {
        return _metadataCache.GetOrAddAsync(dataSource, schema, table, LoadTableMetadataAsync);
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
            // case sensitive
            table = tableMeta.Name;
            schema = tableMeta.Schema;
            _metadataCache.GetOrAddEndpointPath(null, schema, table);
            modelTableInfo = _metadataCache.GetModelInfoFromPath(path);
        }
        if (modelTableInfo == null)
            throw new InvalidOperationException("GetModelInfo fails");
        return modelTableInfo;
    }

    public void RemoveFormFromCache(String? dataSource, String schema, String table, String key)
    {
        _metadataCache.RemoveFormFromCache(dataSource, schema, table, key);
    }
    public Task<FormMetadata> GetFormAsync(String? dataSource, TableMetadata meta, String key, Func<Form> defForm)
    {
        return _metadataCache.GetOrAddFormAsync(dataSource, meta, key, LoadTableFormAsync, defForm);
    }

    public async Task<IEnumerable<ReferenceMember>> ReferenceFieldsAsync(String? dataSource, TableMetadata table)
    {
        async Task<ReferenceMember> CreateMember(TableColumn column, Int32 index)
        {
            var table = column.DataType switch
            {
                ColumnDataType.Operation => await GetSchemaAsync(dataSource, "op", "operations"),
                _ => await GetSchemaAsync(dataSource, column.Reference.RefSchema, column.Reference.RefTable)
            };
            return new ReferenceMember(column, table, index);
        }
        Int32 index = 0;
        var list = new List<ReferenceMember>();
        foreach (var cx in table.Columns.Where(c => c.IsReference && !c.IsEnum))
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
            foreach (var dt in table.Details)
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

    private async Task<FormMetadata> LoadTableFormAsync(String? dataSource, TableMetadata meta, String key, Func<Form> getDefaultForm)
    {
        var prms = new ExpandoObject()
        {
            {"Schema", meta.Schema},
            {"Table", meta.Name},
            {"Key", key},
        };
        var dm = await _dbContext.LoadModelAsync(dataSource, "a2meta.[Table.Form]", prms)
            ?? throw new InvalidOperationException("Form is null");

        var formExpando = dm.Eval<ExpandoObject>("Form.Json");
        if (formExpando == null)
        {
            var defaultForm = getDefaultForm();
            return new FormMetadata(defaultForm, String.Empty);
        }

        // convert Expando to Form
        var json = JsonConvert.SerializeObject(formExpando) 
            ?? throw new InvalidOperationException("Form not found");
        var form = JsonConvert.DeserializeObject<Form>(json, JsonSettings.IgnoreNull)
            ?? throw new InvalidOperationException("Form deserialization fails");

        return new FormMetadata(form, String.Empty);

    }



    private static (String schema, String table) ParsePath(String path)
    {
        var split = path.Split('/');
        if (split.Length < 2 )
            throw new InvalidOperationException($"Invalid path: {path}");
        var schema = split[0].FromFolder();
        return (schema, split[1]);
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Data.Common;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions.Dynamic;
using A2v10.Data.Interfaces;
using A2v10.Data.Core.Extensions;


namespace A2v10.Metadata;

internal class SqlBuilderTags(IServiceProvider serviceProvider, AppPlatformId _platformId)
{
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();

    /* 'Used' says the tag is on at least one record, and the dialog refuses to delete it - so it
     * is computed, never stored: a stored bit would be a second truth to keep in step with the
     * entries table. Not a column of $Tags either, which is what keeps it out of the table type
     * and out of the merge below.
     *
     * The entries table is named by the owner's model, and its schema is always 'cat' - see
     * TableMetadataDefaults.CreateTagEntriesTable. The model reaches SQL as an identifier rather
     * than a parameter because a table name cannot be one; TagEndpointBuilder.TagsFor is what
     * makes that safe, and it is why the check there is not decoration.
     */
    private static String LoadSql(String tagsFor) => $"""

        select [{Constants.FieldNames.Tags}!{TableMetadataDefaults.TagsTypeName()}!Array] = null,
          [Id!!Id] = t.Id, [Name!!Name] = t.[Name], t.[Color], t.[Memo],
          [Used] = cast(case when exists(
            select 1 from {TableMetadataDefaults.TagEntriesTableName(tagsFor)} e where e.[Tag] = t.[Id]
          ) then 1 else 0 end as bit)
        from {TableMetadataDefaults.TagsTableName()} t
        where t.[For] = @{Constants.FieldNames.For}
        order by t.[Id];
        """;

    public Task<IDataModel> LoadTagsModel(String? dataSource, String tagsFor)
    {
        var sql = """
        set nocount on;
        set transaction isolation level read uncommitted;
        """ + LoadSql(tagsFor);
        var prms = new ExpandoObject()
        {
            { Constants.FieldNames.For, tagsFor }
        };
        return _dbContext.LoadModelSqlAsync(dataSource, sql, prms);
    }

    public async Task<ExpandoObject> SaveModelAsync(String? dataSource, ExpandoObject data, String tagsFor)
    {
        var sql = $"""
        set nocount on;
        set transaction isolation level read committed;
        merge {TableMetadataDefaults.TagsTableName()} as t
        using @{Constants.FieldNames.Tags} as s
        on t.[Id] = s.[Id] and t.[For] = @{Constants.FieldNames.For}
        when matched then update set
            t.[Name] = s.[Name],
            t.[Color] = s.[Color],
            t.[Memo] = s.[Memo]
        when not matched then insert([For], [Name], [Color], [Memo]) values
            (@{Constants.FieldNames.For}, s.[Name], s.[Color], s.[Memo])
        when not matched by source and t.[For] = @{Constants.FieldNames.For} then delete;
        """ + LoadSql(tagsFor);

        // the type name comes off the same table the deploy generates it from, so a rename moves both
        var tagsTable = TableMetadataDefaults.TagsTable();
        var dtb = new DataTableBuilder(tagsTable, _platformId);
        var rows = data.Get<List<Object>>(Constants.FieldNames.Tags);

        void SaveParams(DbParameterCollection prms)
        {
            prms.AddString(Constants.FieldNames.For, tagsFor);
            prms.AddStructured(Constants.FieldNames.Tags, tagsTable.SqlTableTypeName, dtb.BuildDataTable(rows));
        }

        var dm = await _dbContext.LoadModelSqlAsync(dataSource, sql, SaveParams)
            ?? throw new InvalidOperationException("Tags. Save failed. DataModel is null");

        return dm.Root;
    }
}

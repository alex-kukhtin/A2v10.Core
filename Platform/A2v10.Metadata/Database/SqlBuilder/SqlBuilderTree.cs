// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.App.Infrastructure;
using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    public async Task<IDataModel> LoadIndexTreeModelAsync()
    {
        var collectionName = Table.RealItemsName;
        var collectionType = Table.RealTypeName;

        var refFields = RefFields; // await ReferenceFieldsAsync(_table));

        var enumFields = DatabaseMetadataProvider.EnumFields(Table, false);
        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        with T(Id, [Name], Icon, HasChildren, [Order], [Parent], InitExpand)
        as (
            select Id = cast(0 as bigint), [Name] = N'{Table.RealItemsLabel.LocalizeSql()}', Icon='folder-outline',
                HasChildren = cast(0 as bit), [Order] = 1, [Parent] = cast(null as bigint),
                [InitExpand] = cast(1 as bit)
            union all
            select Id = cast(-2 as bigint), [Name] = N'[Не в групах]', Icon='folder-ban',
                HasChildren = cast(0 as bit), [Order] = 8, [Parent] = cast(null as bigint),
                [InitExpand] = cast(0 as bit)
            union all
            select Id, [Name], Icon = N'folder-outline',
                HasChildren = case when exists (select * from {Table.SqlTableName} 
                    where Void = 0 and IsFolder = 1 and Parent = f.Id) then 1 else 0 end,
                [Order] = 2, [Parent] = cast(0 as bigint), 
                [InitExpand] = cast(0 as bit)
            from {Table.SqlTableName} f
            where f.IsFolder = 1 and f.Void = 0 and f.Parent is null
        )
        select [Folders!TFolder!Tree] = null, [Id!!Id] = Id, [Name!!Name] = [Name], Icon,
            [SubItems!TFolder!Items] = null, 
            [HasSubItems!!HasChildren] = HasChildren,
            [{collectionName}!{collectionType}!LazyArray] = null,
            [!TFolder.SubItems!ParentId] = T.Parent, [InitExpand!!Expanded] = T.InitExpand
        from T
        order by [Order], [Name];

        -- Lasy table declaration
        select [!{collectionType}!Array] = null, 
            {String.Join(",", Table.AllSqlFields(refFields, enumFields, "c"))},
            [!!RowCount] = 0
        from {Table.SqlTableName} c
        where 0 <> 0;
        """;

        // { RefTableJoins(refFields, "c")} ???

        return await _dbContext.LoadModelSqlAsync(DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
        });
    }

    public Task<IDataModel> ExpandAsync(ExpandoObject expandPrms)
    {
        var collectionName = Table.RealItemsName;
        var collectionType = Table.RealTypeName;

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        select [SubItems!TFolder!Tree] = null, [Id!!Id] = Id, [Name!!Name] = [Name], Icon = N'folder-outline',
            [SubItems!TFolder!Items] = null,
            [HasSubItems!!HasChildren] = case when exists(select 1 from {Table.SqlTableName} c where c.Void=0 and c.Parent = f.Id and c.IsFolder = 1) then 1 else 0 end,
            [{collectionName}!{collectionType}!LazyArray] = null
        from {Table.SqlTableName} f where f.IsFolder=1 and f.Parent = @Id and f.Void=0;

        """;

        return _dbContext.LoadModelSqlAsync(DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddBigInt("@Id", expandPrms.Get<Int64>("Id"));
        });
    }
    public async Task<IDataModel> LoadEditFolderModelAsync()
    {
        throw new NotImplementedException("Not implemented LoadEditFolderModelAsync");
    }

    public async Task<IDataModel> LoadBrowseTreeModelAsync()
    {
        var collectionName = Table.RealItemsName;
        var collectionType = Table.RealTypeName;

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        with T(Id, [Name], Icon, HasChildren, [Order], [Parent], InitExpand)
        as (
            select Id = cast(0 as bigint), [Name] = N'{Table.RealItemsLabel.LocalizeSql()}', Icon='folder-outline',
                HasChildren = cast(0 as bit), [Order] = 1, [Parent] = cast(null as bigint),
                [InitExpand] = cast(1 as bit)
            union all
            select Id, [Name], Icon = N'folder-outline',
                HasChildren = case when exists (select * from {Table.SqlTableName} 
                    where Void = 0 and IsFolder = 1 and Parent = f.Id) then 1 else 0 end,
                [Order] = 2, Parent = cast(0 as bigint),
                [InitExpand] = cast(0 as bit)
            from {Table.SqlTableName} f
            where f.IsFolder = 1 and f.Void = 0 and f.Parent is null
        )
        select [Folders!TFolder!Tree] = null, [Id!!Id] = Id, [Name!!Name] = [Name], Icon,
            [SubItems!TFolder!Items] = null, 
            [HasSubItems!!HasChildren] = HasChildren,
            [!TFolder.SubItems!ParentId] = T.Parent, [InitExpand!!Expanded] = T.InitExpand
        from T
        order by [Order], [Name];

        """;

        return await _dbContext.LoadModelSqlAsync(DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
        });
    }
}

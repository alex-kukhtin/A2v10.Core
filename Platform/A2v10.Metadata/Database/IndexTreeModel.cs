// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;   
using System.Threading.Tasks;
using System.Dynamic;

using A2v10.Data.Interfaces;
using A2v10.Data.Core.Extensions;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
{
    public async Task<IDataModel> LoadIndexTreeModelAsync()
    {
        var collectionName = _table.RealItemsName;
        var collectionType = _table.RealTypeName;

        var refFields = _refFields; // await ReferenceFieldsAsync(_table));

        var enumFields = DatabaseMetadataProvider.EnumFields(_table, false);
        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        with T(Id, [Name], Icon, HasChildren, [Order], [Parent], InitExpand)
        as (
            select Id = cast(0 as bigint), [Name] = N'{_table.RealItemsLabel.LocalizeSql()}', Icon='folder-outline',
                HasChildren = cast(0 as bit), [Order] = 1, [Parent] = cast(null as bigint),
                [InitExpand] = cast(1 as bit)
            union all
            select Id = cast(-2 as bigint), [Name] = N'[Не в групах]', Icon='folder-ban',
                HasChildren = cast(0 as bit), [Order] = 8, [Parent] = cast(null as bigint),
                [InitExpand] = cast(0 as bit)
            union all
            select Id, [Name], Icon = N'folder-outline',
                HasChildren = case when exists (select * from {_table.SqlTableName} 
                    where Void = 0 and IsFolder = 1 and Parent = f.Id) then 1 else 0 end,
                [Order] = 2, [Parent] = cast(0 as bigint), 
                [InitExpand] = cast(0 as bit)
            from {_table.SqlTableName} f
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
            {String.Join(",", _table.AllSqlFields(refFields, enumFields, "c"))},
            [!!RowCount] = 0
        from {_table.SqlTableName} c
        {RefTableJoins(refFields, "c")}
        where 0 <> 0;
        """;

        return await _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
        });
    }

    public Task<IDataModel> ExpandAsync(ExpandoObject expandPrms)
    {
        var collectionName = _table.RealItemsName;
        var collectionType = _table.RealTypeName;

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        select [SubItems!TFolder!Tree] = null, [Id!!Id] = Id, [Name!!Name] = [Name], Icon = N'folder-outline',
            [SubItems!TFolder!Items] = null,
            [HasSubItems!!HasChildren] = case when exists(select 1 from {_table.SqlTableName} c where c.Void=0 and c.Parent = f.Id and c.IsFolder = 1) then 1 else 0 end,
            [{collectionName}!{collectionType}!LazyArray] = null
        from {_table.SqlTableName} f where f.IsFolder=1 and f.Parent = @Id and f.Void=0;

        """;

        return _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
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
        var collectionName = _table.RealItemsName;
        var collectionType = _table.RealTypeName;

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        with T(Id, [Name], Icon, HasChildren, [Order], [Parent], InitExpand)
        as (
            select Id = cast(0 as bigint), [Name] = N'{_table.RealItemsLabel.LocalizeSql()}', Icon='folder-outline',
                HasChildren = cast(0 as bit), [Order] = 1, [Parent] = cast(null as bigint),
                [InitExpand] = cast(1 as bit)
            union all
            select Id, [Name], Icon = N'folder-outline',
                HasChildren = case when exists (select * from {_table.SqlTableName} 
                    where Void = 0 and IsFolder = 1 and Parent = f.Id) then 1 else 0 end,
                [Order] = 2, Parent = cast(0 as bigint),
                [InitExpand] = cast(0 as bit)
            from {_table.SqlTableName} f
            where f.IsFolder = 1 and f.Void = 0 and f.Parent is null
        )
        select [Folders!TFolder!Tree] = null, [Id!!Id] = Id, [Name!!Name] = [Name], Icon,
            [SubItems!TFolder!Items] = null, 
            [HasSubItems!!HasChildren] = HasChildren,
            [!TFolder.SubItems!ParentId] = T.Parent, [InitExpand!!Expanded] = T.InitExpand
        from T
        order by [Order], [Name];

        """;

        return await _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
        });
    }
}

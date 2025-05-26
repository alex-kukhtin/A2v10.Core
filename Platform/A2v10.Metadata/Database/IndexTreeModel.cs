// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;   
using System.Threading.Tasks;
using System.Dynamic;

using A2v10.Data.Interfaces;
using A2v10.Data.Core.Extensions;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    public Task<IDataModel> LoadIndexTreeModelAsync()
    {
        var collectionName = _table.RealItemsName;
        var collectionType = _table.RealTypeName;

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        with T(Id, [Name], Icon, HasChildren, [Order])
        as (
            select Id = cast(-1 as bigint), [Name] = N'[Без групування]', Icon='ban',
                HasChildren = cast(0 as bit), [Order] = 1
            where @StdFolders = 1
            union all
            select Id = cast(-2 as bigint), [Name] = N'[Не в групах]', Icon='folder-ban',
                HasChildren = cast(0 as bit), [Order] = 8
            where @StdFolders = 1
            union all
            select Id, [Name], Icon = N'folder-outline',
                HasChildren = case when exists (select * from {_table.SqlTableName} 
                    where Void = 0 and IsFolder = 1 and Parent = f.Id) then 1 else 0 end,
                [Order] = 2
            from {_table.SqlTableName} f
            where f.IsFolder = 1 and f.Void = 0 and f.Parent is null
        )
        select [Folders!TFolder!Tree] = null, [Id!!Id] = Id, [Name!!Name] = [Name], Icon,
            [SubItems!TFolder!Items] = null, 
            [HasSubItems!!HasChildren] = HasChildren,
            [{collectionName}!{collectionType}!LazyArray] = null
        from T
        order by [Order], [Name];

        -- Lasy table declaration
        select [!{collectionType}!Array] = null, [Id!!Id] = c.Id, [Name!!Name] = c.[Name],
            [Memo], [!!RowCount] = 0
        from {_table.SqlTableName} c
        where 0 <> 0;
        """;

        return _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddBit("@StdFolders", true);
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
}

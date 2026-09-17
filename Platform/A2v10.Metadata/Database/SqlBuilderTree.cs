// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

using A2v10.App.Infrastructure;
using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    public async Task<IDataModel> LoadIndexTreeModelAsync()
    {
        var collectionName = Table.CollectionName;
        var collectionType = Table.TypeName;

        var allColumns = Table.AllColumns().ToList();
        var refs = allColumns.AllRefs().ToList();


        IEnumerable<String> indexSqlFields(String alias)
        {
            static Boolean includeColumn(TableColumn col)
                => col.Type != ColumnType.RowVersion && col.Type != ColumnType.Void;
            return Table.AllColumns(includeColumn).Select(col => col.SqlModelColumnName(alias));
        }

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        with T(Id, [Name], Icon, HasChildren, [Order], [Parent], InitExpand)
        as (
            select Id = cast(0 as platformid), [Name] = N'@[{Table.CollectionName}]', Icon='folder-outline',
                HasChildren = cast(0 as bit), [Order] = 1, [Parent] = cast(null as platformid),
                [InitExpand] = cast(1 as bit)
            union all
            select Id = cast(-2 as platformid), [Name] = N'[Не в групах]', Icon='folder-ban',
                HasChildren = cast(0 as bit), [Order] = 8, [Parent] = cast(null as platformid),
                [InitExpand] = cast(0 as bit)
            union all
            select Id, [Name], Icon = N'folder-outline',
                HasChildren = case when exists (select * from {Table.SqlTableName} 
                    where Void = 0 and IsFolder = 1 and Parent = f.Id) then 1 else 0 end,
                [Order] = 2, [Parent] = cast(0 as platformid), 
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
            {String.Join(",", indexSqlFields("c"))},
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

    /* A chart of accounts, whole, as a tree. Read in one go - a chart is hundreds of rows, not a
     * register - so no lazy expand and no HasChildren.
     *
     * A node is attached to its parent by ParentId, so a parent must come first: the level is counted
     * from Parent by the CTE, never read off the code, where a prefix is a convention of one plan.
     *
     * The closed sets are sent as their localization keys: the column stores the name, the user reads
     * the translation, and a binding cannot compose a key.
     */
    /* 'open' is the tree a picker offers: closed accounts are not candidates. No child hangs from a
     * closed parent - the Parent of a file row is a row of the same file, so it closes with it.
     */
    public async Task<IDataModel> LoadAccountTreeModelAsync(Boolean open)
    {
        var parent = Constants.FieldNames.Parent;
        var items = Constants.FieldNames.Items;
        var onlyOpen = open ? $" and a.[{Constants.FieldNames.Void}] = 0" : String.Empty;

        String Field(TableColumn col) => col.Name is Constants.FieldNames.AccountType or Constants.FieldNames.NormalBalance
            ? $"[{col.Name}] = N'@[{col.Name}.' + a.[{col.Name}] + N']'"
            : col.SqlModelColumnName("a");

        var fields = Table.AllColumns(c => TableColumnPredicates.IsIndexColumn(c) && c.Type != ColumnType.Parent)
            .Select(Field);

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        with T([Id], [Level])
        as (
            select a.[Id], 0 from {Table.SqlTableName} a where a.[{parent}] is null{onlyOpen}
            union all
            select a.[Id], T.[Level] + 1 from {Table.SqlTableName} a inner join T on a.[{parent}] = T.[Id]{onlyOpen}
        )
        select [{Table.CollectionName}!{Table.TypeName}!Tree] = null, {String.Join(", ", fields)},
            [{items}!{Table.TypeName}!Items] = null,
            [!{Table.TypeName}.{items}!ParentId] = a.[{parent}]
        from T inner join {Table.SqlTableName} a on a.[Id] = T.[Id]
        order by T.[Level], a.[Id];
        """;

        return await _dbContext.LoadModelSqlAsync(DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
        });
    }

    public Task<IDataModel> ExpandAsync(ExpandoObject expandPrms)
    {
        var collectionName = Table.CollectionName;
        var collectionType = Table.TypeName;

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
        var collectionName = Table.CollectionName;
        var collectionType = Table.TypeName;

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        with T(Id, [Name], Icon, HasChildren, [Order], [Parent], InitExpand)
        as (
            select Id = cast(0 as platformid), [Name] = N'@[{Table.CollectionName}]', Icon='folder-outline',
                HasChildren = cast(0 as bit), [Order] = 1, [Parent] = cast(null as platformid),
                [InitExpand] = cast(1 as bit)
            union all
            select Id, [Name], Icon = N'folder-outline',
                HasChildren = case when exists (select * from {Table.SqlTableName} 
                    where Void = 0 and IsFolder = 1 and Parent = f.Id) then 1 else 0 end,
                [Order] = 2, Parent = cast(0 as platformid),
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

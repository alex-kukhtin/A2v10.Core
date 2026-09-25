// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    /* The folders of a catalog, whole, as a tree: a catalog has a few dozen of them, never more than
     * a hundred, so no lazy expand and no HasChildren - read the way the chart of accounts is. A node
     * is attached to its parent by ParentId, so a parent must come first: the level is counted by
     * the CTE. A void folder is not in the tree.
     *
     * Every node is a place, and what it shows is what lies directly in it - so the root, the place
     * of the catalog itself, shows what lies in no folder, and the top folders hang under it. The one
     * node that is not a place is All, the view of the whole catalog: first, apart, childless, and
     * nothing is created in it. Neither is a row of $Folders - both are reserved values of the domain
     * (AppPlatformId.All, Root), and the lazy load of the elements reads the same two.
     *
     * CAST takes system types only, so every id is cast to the base the database reported for
     * 'platformid' - the literals, the nulls and the columns alike: the anchor and the recursive
     * member of a CTE must agree on the type exactly, and an alias against its base is not a match
     * to lean on.
     *
     * The elements of a node are a LazyArray on it - there are thousands of them. The picker of a
     * folder (browsefolder) asks for none of that: it picks a value of Folder, and neither All nor
     * the root is one - written into the column, either would fail the foreign key. Its top folders
     * are the roots of the tree; the root place is the cleared selector.
     */
    String FolderTreeSql(Boolean withElements)
    {
        var folders = TableMetadataDefaults.CreateFoldersTable(Table);
        var parent = Constants.FieldNames.Parent;
        var voidCol = Constants.FieldNames.Void;
        String Id(String expr) => $"cast({expr} as {_descr.PlatformId.SqlTypeName})";
        var root = Id($"N'{_descr.PlatformId.Root}'");

        var platformNodes = withElements ? $"""
            select {Id($"N'{_descr.PlatformId.All}'")}, cast(N'[Без групування]' as nvarchar(255)), cast(N'folder-ban' as nvarchar(32)),
                {Id("null")}, 0, 1, cast(0 as bit)
            union all
            select {root}, cast(N'@[{Table.CollectionName}]' as nvarchar(255)), cast(N'folder-outline' as nvarchar(32)),
                {Id("null")}, 0, 2, cast(1 as bit)
            union all
            """ : String.Empty;
        var topParent = withElements ? root : Id("null");
        var elements = withElements
            ? $", [{Table.CollectionName}!{Table.TypeName}!LazyArray] = null"
            : String.Empty;

        return $"""
        with T([Id], [Name], Icon, [Parent], [Level], [Order], InitExpand)
        as (
            {platformNodes}
            select {Id("f.[Id]")}, f.[Name], cast(N'folder-outline' as nvarchar(32)), {topParent}, 1, 2, cast(0 as bit)
            from {folders.SqlTableName} f where f.[{parent}] is null and f.[{voidCol}] = 0
            union all
            select {Id("f.[Id]")}, f.[Name], cast(N'folder-outline' as nvarchar(32)), {Id($"f.[{parent}]")}, T.[Level] + 1, 2, cast(0 as bit)
            from {folders.SqlTableName} f inner join T on f.[{parent}] = T.[Id]
            where f.[{voidCol}] = 0
        )
        select [Folders!TFolder!Tree] = null, [Id!!Id] = T.[Id], [Name!!Name] = T.[Name], T.Icon,
            [SubItems!TFolder!Items] = null,
            [!TFolder.SubItems!ParentId] = T.[Parent], [InitExpand!!Expanded] = T.InitExpand{elements}
        from T
        order by T.[Level], T.[Order], T.[Name];
        """;
    }

    public async Task<IDataModel> LoadIndexTreeModelAsync()
    {
        var collectionType = Table.TypeName;

        IEnumerable<String> indexSqlFields(String alias)
        {
            static Boolean includeColumn(TableColumn col)
                => col.Type != ColumnType.RowVersion && col.Type != ColumnType.Void;
            return Table.AllColumns(includeColumn).Select(col => col.SqlModelColumnName(alias));
        }

        /* The types of the references, declared by maps that are empty here: the rows arrive with the
         * lazy loads, and a RefId there resolves only into a type the page already knows.
         */
        var refMap = new RefMapBuilder(Endpoint, isPlain: false, hasDefaults: false);
        var maps = new StringBuilder();
        if (refMap.GenerateDeclare() is { } declare)
        {
            maps.AppendLine(declare);
            refMap.WriteRefMapIndex(maps);
        }

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        {FolderTreeSql(withElements: true)}

        -- the type of the lazy elements, declared once
        select [!{collectionType}!Array] = null,
            {String.Join(",", indexSqlFields("c"))},
            [!!RowCount] = 0
        from {Table.SqlTableName} c
        where 0 <> 0;

        {maps}

        -- what the filters pick from: the page is loaded once, the lazy loads bring rows only
        {String.Join(Environment.NewLine, ReferencedSets(withDetails: false).Select(en => ValuesRecordset(en, withAll: true)))}
        """;

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

    /* One folder, as its card shows it: the name and the memo - a folder carries nothing else (TRAITS.md
     * §0). Under 'Folder' in the model, the name of the owner's column that points at folders.
     *
     * Parent rides along unshown: a new folder is created where the list's Create put it, and the
     * save needs to know where. Under its model name (ParentElem - 'Parent' is the data model's own
     * word). Not written back by the save of an existing folder: moving one is not a gesture of the
     * card. Icon too, for the node the answer becomes when it is appended into the tree.
     */
    String FolderSelectSql(TableMetadata folders)
    {
        var parent = folders.AllColumns().First(c => c.Type == ColumnType.Parent);
        return $"""
            select [{Constants.FieldNames.Folder}!{folders.TypeName}!MainObject] = null,
                [Id!!Id] = f.[Id], [Name!!Name] = f.[Name], f.[Memo], {parent.SqlModelColumnName("f")},
                Icon = N'folder-outline'
            from {folders.SqlTableName} f where f.[Id] = @Id;
            """;
    }

    /* A new folder starts in the place the list's Create named - the same parameter an element's card
     * reads (InitialSource.Query), since the place is one for both: a folder, or nothing for the root.
     * Cast in SQL to the base, try_cast, so a value that does not parse leaves the folder at the top.
     */
    String NewFolderDefaultsSql(TableMetadata folders)
    {
        var parent = folders.AllColumns().First(c => c.Type == ColumnType.Parent);
        return $"""
            select [!$Defaults!] = null,
                [{Constants.FieldNames.Folder}.{parent.ModelName}] = try_cast(@Query{Constants.FieldNames.Folder} as {PlatformId.SqlTypeName});
            """;
    }

    public async Task<IDataModel> LoadEditFolderModelAsync()
    {
        var folders = TableMetadataDefaults.CreateFoldersTable(Table);
        var isNew = IsNewModel();
        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        {FolderSelectSql(folders)}
        {(isNew ? NewFolderDefaultsSql(folders) : String.Empty)}
        """;

        return await _dbContext.LoadModelSqlAsync(DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddTyped("@Id", PlatformId.SqlDbType, PlatformId.ParseId(_descr.PlatformUrl.Id));
            if (isNew)
                dbprms.AddString($"@Query{Constants.FieldNames.Folder}", QueryValue(Constants.FieldNames.Folder));
        });
    }

    /* A folder is voided, as a record of the catalog is, and only an empty one (TRAITS.md §8.1): with
     * a live subfolder or a live element in it, it would leave them unreachable from the tree - and
     * silently. The server is the judge: what the browser refuses beforehand is a convenience.
     */
    internal async Task<IInvokeResult> DeleteFolderAsync(ExpandoObject? prms)
    {
        var folders = TableMetadataDefaults.CreateFoldersTable(Table);
        var parent = folders.AllColumns().First(c => c.Type == ColumnType.Parent);
        var voidCol = Constants.FieldNames.Void;

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;

        if exists(select 1 from {folders.SqlTableName} where [{parent.Name}] = @Id and [{voidCol}] = 0)
            or exists(select 1 from {Table.SqlTableName} where [{Constants.FieldNames.Folder}] = @Id and [{voidCol}] = 0)
            throw 60000, N'UI:@[Error.Delete.Used]', 0;

        update {folders.SqlTableName} set [{voidCol}] = 1,
            [{Constants.FieldNames.UserModified}] = @UserId, [{Constants.FieldNames.UtcDateModified}] = getutcdate()
        where [Id] = @Id;
        """;

        await _dbContext.LoadModelSqlAsync(DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddTyped("@Id", PlatformId.SqlDbType, PlatformId.ParseId(prms?.Get<Object>("Id")?.ToString()));
        });
        return EmptyInvokeResult.FromString("{}", MimeTypes.Application.Json);
    }

    /* The name and the memo, stamped; a new folder is inserted where it was created (its Parent), an
     * existing one keeps its place. The answer is the folder as the card loads it.
     */
    public async Task<ExpandoObject> SaveFolderModelAsync(ExpandoObject data)
    {
        var folders = TableMetadataDefaults.CreateFoldersTable(Table);
        var parent = folders.AllColumns().First(c => c.Type == ColumnType.Parent);
        var folder = data.Get<ExpandoObject>(Constants.FieldNames.Folder)
            ?? throw new InvalidOperationException($"SaveFolder. '{Constants.FieldNames.Folder}' is not in the model");

        Object? IdOf(Object? value) =>
            AppPlatformId.IsEmpty(value) ? null : PlatformId.ParseId(value!.ToString());

        var name = Constants.FieldNames.Name;
        var memo = Constants.FieldNames.Memo;
        var sqlString = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;

        if @Id is null
        begin
            declare @rtable table([Id] platformid);
            insert into {folders.SqlTableName} ([{name}], [{memo}], [{parent.Name}],
                [{Constants.FieldNames.UserCreated}], [{Constants.FieldNames.UtcDateCreated}],
                [{Constants.FieldNames.UserModified}], [{Constants.FieldNames.UtcDateModified}])
            output inserted.[Id] into @rtable([Id])
            values (@Name, @Memo, @Parent, @UserId, getutcdate(), @UserId, getutcdate());
            select @Id = [Id] from @rtable;
        end
        else
            update {folders.SqlTableName} set
                [{name}] = @Name, [{memo}] = @Memo,
                [{Constants.FieldNames.UserModified}] = @UserId, [{Constants.FieldNames.UtcDateModified}] = getutcdate()
            where [Id] = @Id;

        {FolderSelectSql(folders)}
        """;

        var dm = await _dbContext.LoadModelSqlAsync(DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddTyped("@Id", PlatformId.SqlDbType, IdOf(folder.Get<Object>("Id")));
            dbprms.AddTyped("@Parent", PlatformId.SqlDbType, IdOf(folder.Get<Object>(parent.ModelName)));
            dbprms.AddString("@Name", folder.Get<String>(name));
            dbprms.AddString("@Memo", folder.Get<String>(memo));
        });
        return dm.Root;
    }

    public async Task<IDataModel> LoadBrowseTreeModelAsync()
    {
        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        {FolderTreeSql(withElements: false)}
        """;

        return await _dbContext.LoadModelSqlAsync(DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
        });
    }
}

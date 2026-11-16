// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;

using static A2v10.Metadata.SqlExtensions;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    /* A folder picked by typing, at the owner's address: the rows are the owner's $Folders. The same
     * answer the fetch below gives, without its extras - a folder is a name and nothing else.
     */
    internal async Task<IInvokeResult> FetchFolderAsync(ExpandoObject? prms)
    {
        var folders = TableMetadataDefaults.CreateFoldersTable(Table);
        var sql = $"""
            set nocount on;
            set transaction isolation level read uncommitted;
            {GateSql(Gate.View)}

            declare @fr nvarchar(255);
            set @fr = N'%' + @Text + N'%';

            select top(100) [{folders.CollectionName}!{folders.TypeName}!Array] = null,
                [Id!!Id] = a.Id, [Name!!Name] = a.[{folders.Presentation}]
            from {folders.SqlTableName} a
            where a.[Void] = 0 and a.[{folders.Presentation}] like @fr
            order by a.[{folders.Presentation}];
            """;

        var model = await _dbContext.LoadModelSqlAsync(DataSource, sql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Text", prms?.Get<String>("Text"));
        });

        return model.ToInvokeResult();
    }

    /* Which columns beyond Id and Name the answer has to carry, asked for by the caller.
     *
     * It has to be asked for: this endpoint is the catalog, and the catalog does not know who is
     * picking from it. 'inherit' is declared on the ROW that holds the reference, and the same
     * catalog is picked from by rows that inherit different things or nothing at all.
     *
     * What travels is the name of the column HERE - InheritDescriptor.Source - not the name of
     * the field it lands in over there. The two coincide often enough to be a trap.
     *
     * The list arrives from the browser, so it is resolved against the shape and never reaches
     * the query as text. That also gives the right answer when a generated view outlives the
     * column it names: a message, not a silently missing field.
     */
    private TableColumn[] FetchColumns(String? inherit)
    {
        if (String.IsNullOrEmpty(inherit))
            return [];
        return [.. inherit
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .Select(name => Table.AllColumns().FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException(
                    $"fetch: column '{name}' not found in {Table.SqlTableName}"))];
    }

    /* A reference among them is resolved the way the index resolves its own - RefId in the row, the
     * object itself in a Map recordset - and BY the same resolve (RefMapBuilder.GenerateResolves): the
     * object the selector hands to the row has the shape the document load hands to it. The fetch's own
     * @map names its slots after these columns, which is what the resolve reads. A second resolve kept
     * here once sent no Role, Done, $Url or $Icon, and read a document's table instead of its view.
     */
    private static String FetchMaps(TableMetadata table, List<TableColumn> refColumns) =>
        new RefMapBuilder([(table, String.Empty, refColumns)]).GenerateResolves() ?? String.Empty;

    /* The owners the picker sends (ScriptBuilder.OwnerProperties) - the browse gets the same ones
     * through its url, as filters of the index. Only owner columns are read: the arguments come
     * from the browser, and nothing else narrows a fetch. An empty id is no owner, as an absent one.
     */
    internal static List<(TableColumn Column, Object Id)> FetchOwners(TableMetadata table, AppPlatformId platformId, ExpandoObject? prms) =>
        [.. table.OwnerColumns()
            .Select(c => (Column: c, Id: platformId.ParseId(prms?.Get<Object>(c.Name)?.ToString())))
            .Where(o => !AppPlatformId.IsEmpty(o.Id))
            .Select(o => (o.Column, o.Id!))];

    internal async Task<IInvokeResult> FetchAsync(ExpandoObject? prms) =>
        await ChoiceAsync(FetchSqlText(prms), prms, prms?.Get<String>("Text"));

    /* An 'equal' channel typed into: the one record whose column is the text, or none
     * (TableMetadata.Lookup). The channel is named, because two exact channels would answer two
     * different questions about one text; it is resolved against the shape like the fetch's columns.
     */
    internal async Task<IInvokeResult> LookupAsync(ExpandoObject? prms) =>
        await ChoiceAsync(LookupSqlText(prms), prms, prms?.Get<String>("Text")?.Trim());

    private async Task<IInvokeResult> ChoiceAsync(String sql, ExpandoObject? prms, String? text)
    {
        var owners = FetchOwners(Table, PlatformId, prms);

        var model = await _dbContext.LoadModelSqlAsync(DataSource, sql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Text", text);
            foreach (var (column, id) in owners)
                dbprms.AddTyped(ColumnParam(column.Name), PlatformId.SqlDbType, id);
        });

        return model.ToInvokeResult();
    }

    /* A fetch is a place of choice: searched by every column the target is chosen by - DisplayAs and
     * the 'contains' channels by a substring, an 'equal' channel by the whole text - and ordered by
     * the one the selector shows (DisplayAs).
     */
    internal String FetchSqlText(ExpandoObject? prms)
    {
        var match = String.Join(" or ", Table.Lookup
            .Select(l => l.Match == LookupMatch.Equal ? $"a.[{l.Column.Name}] = @Text" : $"a.[{l.Column.Name}] like @fr")
            .Prepend($"a.[{Table.DisplayAs}] like @fr"));
        return ChoiceSqlText(prms, match, 100, String.Empty);
    }

    // the text of the exact search, built without the database
    internal String LookupSqlText(ExpandoObject? prms)
    {
        var name = prms?.Get<String>("Channel");
        var channel = Table.Lookup.FirstOrDefault(l => l.Column.Name == name && l.Match == LookupMatch.Equal)
            ?? throw new InvalidOperationException(
                $"lookup: '{name}' is no 'equal' channel of {Table.Path}. Equal: {String.Join(", ", Table.Lookup.Where(l => l.Match == LookupMatch.Equal).Select(l => l.Column.Name))}");
        // 'unique' is checked at input, with no index under it (ISSUES 3.9): a duplicate written past the form is an error, never the first one
        var guard = $"""

            if (select count(*) from @map) > 1
                {UserError("Error.Lookup.NotUnique")}
            """;
        return ChoiceSqlText(prms, $"a.[{channel.Column.Name}] = @Text", 2, guard);
    }

    /* The two answers share everything but the predicate: the rows of this address, and an element
     * of one shape however it reached the record - Name the presentation, the choice column, the
     * colour, the channels (RefMapBuilder.CarriedColumns has them in the card), and the columns the
     * picking row inherits. Two shapes of the text, and the difference is real: without references
     * there is nothing to resolve and one select answers; with them the rows are needed twice, so
     * they are materialized once instead of being selected twice under the predicate. A guard reads
     * the materialized rows, so it always takes the second shape.
     */
    private String ChoiceSqlText(ExpandoObject? prms, String match, Int32 top, String guard)
    {
        var shown = Table.Presentation;
        var chosen = Table.DisplayAs;
        var carried = ChoiceField(Table, "a") + ColorField(Table, "a")
            + String.Concat(ChannelColumns().Select(c => $", a.[{c.Name}]"));
        // what the picking row inherits, but not twice: an inherit may read a channel or the choice column
        String?[] sent = [Table.ChoiceProperty, Table.ColorColumn?.Name, .. ChannelColumns().Select(c => c.Name)];
        var columns = FetchColumns(prms?.Get<String>("inherit"));
        var extra = String.Join("", columns
            .Where(c => !sent.Contains(c.Name))
            .Select(c => $",\n    {c.SqlModelColumnName("a")}"));

        var refColumns = columns.Where(c => c.IsRef).ToList();
        var owners = FetchOwners(Table, PlatformId, prms);
        // the same rows the browse dialog of this address shows - see FixedPredicate
        var fixedRows = FixedPredicate("a")
            + String.Concat(owners.Select(o => $" and {OwnerMatch($"a.[{o.Column.Name}]", ColumnParam(o.Column.Name))}"));
        // the rows of this address, as the index has them: over a shared table, this document's alone
        var own = OwnOperations("a") is { } ownRows ? $" and {ownRows}" : String.Empty;

        if (refColumns.Count == 0 && String.IsNullOrEmpty(guard))
            return $"""
            set nocount on;
            set transaction isolation level read uncommitted;
            {GateSql(Gate.View)}

            declare @fr nvarchar(255);
            set @fr = N'%' + @Text + N'%';

            select top({top}) [{Table.CollectionName}!{Table.TypeName}!Array] = null,
                [Id!!Id] = a.Id, [Name!!Name] = a.[{shown}]{carried}{extra}
            from {Table.SqlTableName} a
            where a.[Void] = 0{fixedRows}{own} and
                ({match})
            order by a.[{chosen}];
            """;

        var mapColumns = String.Concat(refColumns.Select(c => $", [{c.Name}] {c.SqlDataType()}"));
        var mapNames = String.Concat(refColumns.Select(c => $", [{c.Name}]"));
        var mapValues = String.Concat(refColumns.Select(c => $", a.[{c.Name}]"));
        return $"""
            set nocount on;
            set transaction isolation level read uncommitted;
            {GateSql(Gate.View)}

            declare @fr nvarchar(255);
            set @fr = N'%' + @Text + N'%';

            declare @map table(Id platformid{mapColumns});

            insert into @map(Id{mapNames})
            select top({top}) a.Id{mapValues}
            from {Table.SqlTableName} a
            where a.[Void] = 0{fixedRows}{own} and
                ({match})
            order by a.[{chosen}];
            {guard}
            select [{Table.CollectionName}!{Table.TypeName}!Array] = null,
                [Id!!Id] = a.Id, [Name!!Name] = a.[{shown}]{carried}{extra}
            from {Table.SqlTableName} a
                inner join @map m on m.Id = a.Id
            order by a.[{chosen}];

            {FetchMaps(Table, refColumns)}
            """;
    }

    // the channels the element carries beyond RefColumns - the ones a map sends in the card (RefMapBuilder.CarriedColumns)
    private IEnumerable<TableColumn> ChannelColumns() =>
        Table.LookupColumns.Where(c => c.Name != Constants.FieldNames.Name && c.Name != Table.ChoiceProperty);
}

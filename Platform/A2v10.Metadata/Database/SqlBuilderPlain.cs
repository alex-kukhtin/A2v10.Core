// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Core.Extensions.Dynamic;
using A2v10.Data.Interfaces;

namespace A2v10.Metadata;

/* Where the load reads a record and its rows: each table by name, the record by its id - or, at birth
 * on basis, table variables of the same shape, filled from the source, and the empty key the new
 * record carries (SqlBuilder.BuildBirthSqlTextAsync). Everything that reads rows - the recordsets, the
 * map - asks this, so a born record is loaded by the same text as a stored one.
 */
internal sealed record LoadRows(Func<TableMetadata, String> From, String Key)
{
    internal static readonly LoadRows Stored = new(t => t.SqlTableName, "@Id");
}

internal partial class SqlBuilder
{

    Boolean IsNewModel()
    {
        var id = _descr.PlatformUrl.Id;
        if (String.IsNullOrWhiteSpace(id) || id == "new")
            return true;
        return false;
    }

    /* The initial values a new record takes from the url that opened it (InitialSource.Query). The
     * value arrives from the browser, so it never enters the SQL as text: a string parameter named
     * after the FIELD - a column name, already an identifier; the declared value is only the key it
     * is looked up by. Cast in SQL to the column's type with try_cast, so a value that does not parse
     * leaves the field empty, as an absent one does. Declared once, before the map, and read by both
     * the map (RefMapBuilder.GenerateInitialRefs) and the defaults.
     */
    IReadOnlyList<(String Key, String Param)> QueryInitials() =>
        IsNewModel()
            ? [.. Endpoint.AllInitials().Where(x => x.Value.Source == InitialSource.Query).Select(x => (x.Key, x.Value.Value))]
            : [];

    TableColumn QueryColumn(String key) =>
        Table.AllColumns().FirstOrDefault(c => c.Name == key)
            ?? throw new InvalidOperationException($"initialValues: '{key}' is not a column of {Table.SqlTableName}");

    String? QueryInitialsSql()
    {
        var initials = QueryInitials();
        if (initials.Count == 0)
            return null;
        return String.Join(Environment.NewLine, initials.Select(q =>
        {
            var column = QueryColumn(q.Key);
            // CAST takes system types only: an identifier is cast to the base the database reported
            var castTo = column.ToSqlDbTypeInfo().SqlName == "platformid" ? _descr.PlatformId.SqlTypeName : column.SqlDataType();
            return $"declare @Init{q.Key} {column.SqlDataType()} = try_cast(@Query{q.Key} as {castTo});";
        }));
    }

    // the query keeps the name as the url spelled it, and a client may lower it
    String? QueryValue(String name)
    {
        var query = _descr.PlatformUrl.Query;
        return (query?.Get<Object>(name) ?? query?.Get<Object>(name.ToLowerInvariant()))?.ToString();
    }

    // '?Op=' carries the name the file uses; the column holds the code (MetadataExtensions.StartOperation)
    String? QueryInitialValue(String key, String param) =>
        QueryColumn(key).IsOperation ? Endpoint.StartOperation(QueryValue(param)) : QueryValue(param);

    internal String BuildLoadPlainSqlText() =>
        BuildLoadPlainSqlText(LoadRows.Stored, birth: null,
            IsNewModel() ? GateSql(Gate.Create) : GateSql(Gate.View) + RecordCheck("@Id"));

    /* 'birth' fills the table variables 'rows' reads (BirthPrelude), so it runs before everything that reads the rows.
     * 'head' - the gate and the record check - is empty inside a save: the text is its tail there, and the
     * save's own are at its head.
     */
    String BuildLoadPlainSqlText(LoadRows rows, String? birth, String head)
    {
        var allColumns = Table.AllColumns().ToList();
        // var refs = allColumns.AllRefs().ToList();

        IEnumerable<String> plainSqlFields(String alias)
        {
            static Boolean includeColumn(TableColumn col)
                => col.Type != ColumnType.Void;
            return Table.AllColumns(includeColumn).Select(col => col.SqlModelColumnName(alias));
        }

        String mainDetailsFields(KeyValuePair<String, TableMetadata> detail)
        {
            var dt = detail.Value;
            if (dt.Kinds.Count == 0)
                return $"[{detail.Key}!{dt.TypeName}!Array] = null";
            else
                return String.Join(", ", dt.Kinds.Keys.Select(
                    k => $"[{dt.KindCollectionName(k)}!{dt.KindTypeName(k)}!Array] = null"));
        }

        /* The record's own tags, and the list it may pick from. The same pair the index emits and
         * under the same names: '{Model}.Tags' is what the editor binds to, the root 'Tags' is its
         * candidates.
         */
        String tagsRecordsets() => $"""
            -- tags
            select [!{TableMetadataDefaults.TagsTypeName()}!Array] = null, [Id!!Id] = t.Id,
                [Name!!Name] = t.[Name], t.[Color], t.[Memo],
                [!{Table.TypeName}.{Constants.FieldNames.Tags}!ParentId] = e.[{Table.Model}]
            from {TableMetadataDefaults.TagEntriesTableName(Table.Model)} e
                inner join {TableMetadataDefaults.TagsTableName()} t on t.[Id] = e.[Tag]
            where e.[{Table.Model}] = @Id and t.[For] = N'{Table.Model}';

            select [{Constants.FieldNames.Tags}!{TableMetadataDefaults.TagsTypeName()}!Array] = null,
                [Id!!Id] = t.Id, [Name!!Name] = t.[Name], t.[Color], t.[Memo]
            from {TableMetadataDefaults.TagsTableName()} t where t.[For] = N'{Table.Model}'
            order by t.[Id];
            """;

        String? generateDefaults()
        {
            if (!IsNewModel())
                return null;
            // everything a new record of THIS address starts on, declared and derived alike
            var initValues = Endpoint.AllInitials();
            if (initValues.Count == 0)
                return null;

            String getDefaultProfile(String key)
            {
                var column = Table.Columns.FirstOrDefault(c => c.Name == key)
                    ?? throw new InvalidOperationException($"Column {key} not found in {Table.SqlTableName}");
                return $"[{Table.Model}.{key}!{column.RefTableCheck.Storage.RefTypeName}!RefId] = @Init{key}";
            }

            /* A value, whoever decided it - the file, the endpoint's own operation, the set's
             * initial state. A reference is written by its KEY and comes back as an object, so it
             * is emitted as a RefId and resolved through the map; the map is filled for it by
             * RefMapBuilder.GenerateInitialRefs, from the same list, and without that the control
             * opens empty.
             */
            String getDefaultLiteral(String key, String value)
            {
                // AllColumns: a fixed field may be one the kind adds (Done, IsSystem), not only a declared one
                var column = Table.AllColumns().FirstOrDefault(c => c.Name == key)
                    ?? throw new InvalidOperationException($"Column {key} not found in {Table.SqlTableName}");
                return column.IsRef
                    ? $"[{Table.Model}.{key}!{column.RefTableCheck.Storage.RefTypeName}!RefId] = {column.SqlLiteral(value)}"
                    : $"[{Table.Model}.{key}] = {column.SqlLiteral(value)}";
            }

            /* '$operation$' used to be a case here. It was the platform telling itself a fact it
             * already knew, through a marker in a value, and it had to be spelled a second time in
             * the map - so the operation is now an ordinary literal (MetadataExtensions.AllInitials)
             * and this reads what a file can actually write.
             */
            String getDefaultContext(String key, String value)
            {
                return value switch
                {
                    Constants.ContextValues.Today => $"[{Table.Model}.{key}!!Utc] = a2meta.fn_getUtcDate()",
                    _ => throw new InvalidOperationException($"Invalid initial context value '{value}'")
                };
            }

            // the variable QueryInitialsSql declared - a reference comes back as an object, as a literal does
            String getDefaultQuery(String key)
            {
                var column = QueryColumn(key);
                return column.IsRef
                    ? $"[{Table.Model}.{key}!{column.RefTableCheck.Storage.RefTypeName}!RefId] = @Init{key}"
                    : $"[{Table.Model}.{key}] = @Init{key}";
            }

            var sb = new StringBuilder("select [!$Defaults!] = null, ");

            sb.AppendJoin(", ", initValues.Select(p =>
                p.Value.Source switch
                {
                    InitialSource.Profile => getDefaultProfile(p.Key),
                    InitialSource.Literal => getDefaultLiteral(p.Key, p.Value.Value),
                    InitialSource.Context => getDefaultContext(p.Key, p.Value.Value),
                    InitialSource.Query => getDefaultQuery(p.Key),
                    _ => throw new InvalidOperationException($"Invalid initial source {p.Value.Source}")
                }
            ));
            sb.AppendLine(";");
            return sb.ToString();
        }

        var sb = new StringBuilder($"""
            -- load for {Table.Model}

            set nocount on;
            set transaction isolation level read uncommitted;
            {head}

            """);
        sb.AppendLine();

        if (birth != null)
        {
            sb.AppendLine(birth);
            sb.AppendLine();
        }

        /* STEP 1: main recordset. 'MainObject', not 'Object': the record this page edits. The reader
         * fills TRoot.MainObject and IDataModel.MainElement by it, the client gets '$main'.
         */
        sb.AppendLine("-- main recordset");

        sb.Append($"""
            select [{Table.Model}!{Table.TypeName}!MainObject] = null, {String.Join(", ", plainSqlFields("a"))}
            """);
        // slots the object carries beyond its own columns: one per collection, and the tags array
        List<String> arraySlots = [.. Table.Details.Select(mainDetailsFields)];
        if (Table.HasTags)
            arraySlots.Add(
                $"[{Constants.FieldNames.Tags}!{TableMetadataDefaults.TagsTypeName()}!Array] = null");

        if (arraySlots.Count > 0)
        {
            sb.AppendLine(",");
            sb.Append("  ");
            sb.AppendJoin(", ", arraySlots);
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine();
        }
        sb.AppendLine($"from {rows.From(Table)} a where a.Id = {rows.Key};");


        if (Table.Details.Count > 0)
        {
            // STEP 2: DETAILS
            sb.AppendLine();
            foreach (var d in Table.Details)
            {
                var dt = d.Value;

                static Boolean includeDetailsColumn(TableColumn col)
                    => col.Type != ColumnType.RowKind && col.Type != ColumnType.Id;

                var detailsFields = dt.Columns.Where(c => includeDetailsColumn(c)).Select(col => col.SqlModelColumnName("d")).ToList();

                /* One recordset per kind, each with its own type and its own collection in the
                 * envelope. The discriminator never leaves the server: a row's kind is the
                 * collection it arrives in, which is why the filter sits here and RowKind is
                 * not among the columns sent (includeDetailsColumn).
                 *
                 * A collection without kinds is the same shape with a single pass - so the two
                 * cases differ in the list, not in the emitting code below.
                 */
                List<(String Type, String Parent, String Filter)> passes = dt.Kinds.Count > 0
                    ? [.. dt.Kinds.Keys.Select(k => (dt.KindTypeName(k), dt.KindCollectionName(k),
                        $" and d.[{dt.RowKindField}] = N'{k}'"))]
                    : [(dt.TypeName, d.Key, String.Empty)];

                foreach (var (type, parent, filter) in passes)
                {
                    sb.AppendLine($"""
                    select [!{type}!Array] = null, [Id!!Id] = d.Id, [RowNo!!RowNumber] = d.RowNo,
                    """);
                    if (detailsFields.Count > 0)
                    {
                        sb.Append($"  {String.Join(", ", detailsFields)}");
                        sb.AppendLine(",");
                    }
                    sb.AppendLine($"""
                      [!{Table.TypeName}.{parent}!ParentId] = d.[{dt.MasterField}]
                    from {rows.From(dt)} d where d.[{dt.MasterField}] = {rows.Key}{filter}
                    order by d.RowNo;
                    """);
                }
            }
        }


        if (Table.HasTags)
        {
            sb.AppendLine();
            sb.AppendLine(tagsRecordsets());
        }

        // before the map: it reads these for the references among them
        if (QueryInitialsSql() is { } queryInitials)
        {
            sb.AppendLine();
            sb.AppendLine(queryInitials);
        }

        var refMap = new RefMapBuilder(Endpoint, isPlain: true, hasDefaults: IsNewModel(), rows);

        // STEP 3: map recordsets

        refMap.WriteRefMap(sb);

        // without the 'All' row: a record picks a value, and 'all of them' is not one
        foreach (var en in ReferencedSets(withDetails: true))
        {
            sb.AppendLine();
            sb.AppendLine(ValuesRecordset(en, withAll: false));
        }

        /* The operations a document switches between ride with the record, as a set's values do: the
         * living ones of THIS document, in the order its file lists them. A document on an operation
         * that has since become void still shows its name - that comes through the map.
         */
        if (Endpoint.Declaration.OperationDeclarations.Count > 0)
        {
            var registry = Table.AllColumns().First(c => c.IsOperation).RefTableCheck.Storage;
            sb.AppendLine();
            sb.AppendLine($"""
                -- operations of the document
                select [{registry.CollectionName}!{registry.TypeName}!Array] = null,
                    [Id!!Id] = e.[{Constants.FieldNames.Id}], [Name!!Name] = e.[{Constants.FieldNames.Name}]
                from {registry.SqlTableName} e
                where e.[{Constants.FieldNames.Document}] = N'{Endpoint.Name}' and e.[{Constants.FieldNames.Void}] = 0
                order by e.[{Constants.FieldNames.Order}];
                """);
        }

        var defs = generateDefaults();
        if (defs != null) {
            sb.AppendLine();
            sb.AppendLine(defs);
        }

        /* STEP 5: system recorset. What makes a record read-only: a posted document, a row the
         * deploy writes (IsSystem - editing it is forbidden whole, see CLAUDE.md, "Chart of accounts").
         */
        var readOnly = Table.IsDocument ? "a.[Done]"
            : Table.AllColumns().Any(c => c.Type == ColumnType.IsSystem) ? $"a.[{Constants.FieldNames.IsSystem}]"
            : null;
        if (readOnly != null)
        {
            sb.AppendLine();
            sb.AppendLine("-- system recordset");
            sb.Append($"""
                select [!$System!] = null, [!!ReadOnly] = {readOnly}
                from {Table.SqlTableName} a where a.Id = @Id;
                """);
        }
        return sb.ToString();
    }

    public async Task<IDataModel> LoadPlainModelAsync()
    {
        var basedOn = BasedOnQueryValue();
        var sqlQuery = basedOn == null ? BuildLoadPlainSqlText() : await BuildBirthSqlTextAsync();
        var basisId = basedOn == null ? null : PlatformId.ParseId(basedOn)
            ?? throw new InvalidOperationException($"{Endpoint.Path}: '?{Constants.FieldNames.BasedOnQuery}={basedOn}' is not an id");

        // a document listing no operations declares no query initial for the column, so nothing else would read '?Op=' to refuse it
        if (IsNewModel() && Endpoint.Declaration.OperationDeclarations.Count == 0)
            Endpoint.StartOperation(QueryValue(Constants.FieldNames.OperationQuery));

        return await _dbContext.LoadModelSqlAsync(DataSource, sqlQuery, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddString("@Id", _descr.PlatformUrl.Id);
            foreach (var (key, param) in QueryInitials())
                dbprms.AddString($"@Query{key}", QueryInitialValue(key, param));
            if (basisId != null)
                dbprms.AddTyped("@BasedOn", PlatformId.SqlDbType, basisId);
        });
    }

    /* A reference to an owned record, held against the owner its picker filtered by
     * (MetadataExtensions.OwnerLinks): a record owned by someone else is refused, and one owned by
     * nobody too. An empty field checks nothing, as it filtered nothing. Over what was SENT and
     * before the merge: the picker is the browser's half, this is the half that is not.
     */
    internal static String OwnerCheck(TableMetadata table)
    {
        var sb = new StringBuilder();

        void check(String source, TableMetadata scope, TableMetadata? header)
        {
            foreach (var reference in scope.AllColumns())
            {
                var links = reference.OwnerLinks(scope, header);
                if (links.Count == 0)
                    continue;
                var mismatch = links.Select(l =>
                {
                    var value = $"{(l.Header ? "h" : "s")}.[{l.Field.Name}]";
                    return $"{value} is not null and (r.[{l.Owner.Name}] is null or r.[{l.Owner.Name}] <> {value})";
                });
                var joinHeader = links.Any(l => l.Header) ? $" cross join @{table.Model} h" : String.Empty;
                sb.AppendLine($"""
                if exists(select 1 from {source} s{joinHeader}
                    inner join {reference.RefTableCheck.Storage.SqlTableName} r on r.[Id] = s.[{reference.Name}]
                    where {String.Join(" or ", mismatch.Select(m => $"({m})"))})
                    throw 60000, N'UI:@[Error.Owner]', 0;
                """);
            }
        }

        check($"@{table.Model}", table, null);
        // the parameters the save sends: one per kind, or one per collection (SavePlainModelAsync)
        foreach (var (collection, rows) in table.Details)
        {
            IEnumerable<String> sources = rows.Kinds.Count > 0 ? rows.Kinds.Keys.Select(rows.KindCollectionName) : [collection];
            foreach (var source in sources)
                check($"@{source}", rows, table);
        }
        return sb.ToString();
    }

    // the record the save sends is the one row of the table-valued parameter named after the model
    String SentId => $"(select [{Constants.FieldNames.Id}] from @{Table.Model})";

    internal Gate SaveGate() => Gate.Save(Table.SqlTableName, SentId);

    /* What was sent keeps an operation of this document: a foreign one would move the row into
     * another document, or create one of it under this document's gate.
     */
    internal String SentOperationCheck() => OwnOperations("s") is { } own
        ? $"""
        if exists(select 1 from @{Table.Model} s where not ({own}))
            throw 60000, N'UI:@[UIError.AccessDenied]', 0;
        """
        : String.Empty;

    /* The rows of every collection, merged into their tables. Matched by Id AND by the master: an id is per
     * table, so without the master a row id of another document would update that document's row through
     * this one's gate. Unmatched, it is inserted as a new row of this document; the foreign row is untouched.
     */
    internal String MergeDetailsSql()
    {
        if (Table.Details == null || Table.Details.Count == 0)
            return String.Empty;
        var sb = new StringBuilder("-- merge details");
        sb.AppendLine();

        Boolean updateablePredicate(TableColumn c)
            => c.Type != ColumnType.Master && c.Type != ColumnType.RowKind && c.Type != ColumnType.Id && !c.HasSqlAs;

        String mergeOneDetails(TableMetadata detailsTable, String key)
        {
            var updateFields = detailsTable.AllColumns(updateablePredicate);

            return $"""
				merge {detailsTable.SqlTableName} as t
				using @{key} as s
				on t.[Id] = s.[Id] and t.[{detailsTable.MasterField}] = @Id
				when matched then update set
				    {String.Join(',', updateFields.Select(f => $"t.[{f.Name}] = s.[{f.Name}]"))}
				when not matched then insert
				    ([{detailsTable.MasterField}], {String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
				    (@Id, {String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
				when not matched by source and t.[{detailsTable.MasterField}] = @Id then delete;
				""";
        }

        /* One table, N table-valued parameters - one per kind, since that is how the model
         * splits. The kind itself is never sent: it is a literal here, so a row cannot
         * arrive claiming a kind other than the collection it came in, and it is excluded
         * from 'update set' (updateablePredicate) so an existing row never changes kind.
         *
         * The delete pass is limited to the DECLARED kinds. Without that, a row whose kind
         * was removed from the metadata - and which therefore arrives in no parameter -
         * matches nothing in the source and is deleted on the next save of the document.
         * Bounded this way it is only orphaned, and an orphan can still be recovered.
         */
        String mergeMultiDetails(TableMetadata detailsTable)
        {
            var updateFields = detailsTable.AllColumns(updateablePredicate);
            var kindField = detailsTable.Columns.FirstOrDefault(c => c.Type == ColumnType.RowKind)
                ?? throw new InvalidOperationException("Kind field not found");

            var usingDetails = detailsTable.Kinds.Keys.Select(k =>
                $"select [$Kind] = N'{k}', * from @{detailsTable.KindCollectionName(k)}"
            );
            var declaredKinds = String.Join(", ", detailsTable.Kinds.Keys.Select(k => $"N'{k}'"));

            return $"""
				with ST as (
				    {String.Join("\nunion all\n", usingDetails)}
				)
				merge {detailsTable.SqlTableName} as t
				using ST as s
				on t.[Id] = s.[Id] and t.[{detailsTable.MasterField}] = @Id
				when matched then update set
					{String.Join(',', updateFields.Select(f => $"t.[{f.Name}] = s.[{f.Name}]"))}
				when not matched then insert
					([{detailsTable.MasterField}], [{kindField.Name}], {String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
					(@Id, s.[$Kind], {String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
				when not matched by source and t.[{detailsTable.MasterField}] = @Id and t.[{kindField.Name}] in ({declaredKinds}) then delete;
				""";
        }

        foreach (var details in Table.Details)
        {
            if (details.Value.Kinds.Count == 0)
                sb.AppendLine(mergeOneDetails(details.Value, details.Key));
            else
                sb.AppendLine(mergeMultiDetails(details.Value));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public async Task<ExpandoObject> SavePlainModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {
        String CheckRowVersion()
        {
            if (Table.AllColumns(c => c.Type == ColumnType.RowVersion).Any())
            {
                var elemName = Table.IsDocument ? "Document" : "Element";
                return $"""
                    if exists(select * from @{Table.Model} t inner join {Table.SqlTableName} c on c.Id = t.Id
                        where t.rv is not null and t.rv <> c.rv)
                    throw 60000, N'UI:@[Error.{elemName}.RowVersion]', 0;
                """;
            }
            return String.Empty;
        }


        /* Tags are not a detail: no fields of their own, no RowNo, nothing to update - a row either
         * is in the set or is not. So the merge has no 'when matched' arm, and what is deleted is
         * bounded by the master exactly as the details merges bound theirs.
         *
         * The parameter is the platform's, so it lives under '$' (CLAUDE.md, "Names"): the record's
         * own TVP and its collections' are named after the model and the collection keys, and an
         * author's 'Tags' collection would have declared @Tags a second time.
         */
        String MergeTags()
        {
            if (!Table.HasTags)
                return String.Empty;
            return $"""
            -- merge tags
            merge {TableMetadataDefaults.TagEntriesTableName(Table.Model)} as t
            using {TagsParam} as s
            on t.[{Table.Model}] = @Id and t.[Tag] = s.[Id]
            when not matched then insert ([{Table.Model}], [Tag]) values (@Id, s.[Id])
            when not matched by source and t.[{Table.Model}] = @Id then delete;
            """;
        }

        /* Where a numbering is declared, the merge is asked for two more things about the row it
         * wrote: whether it inserted one, and what stood in the number column afterwards. Both
         * answers are about what LANDED - reading the parameter again would answer what was sent,
         * which is a different question and takes its own scan to ask.
         *
         * The pair is checked at load (DatabaseMetadataProvider.CheckAutonumColumn), so First()
         * here cannot come up empty.
         */
        var autonumColumn = String.IsNullOrEmpty(Endpoint.Declaration.Autonum)
            ? null
            : Table.AllColumns().First(c => c.Type == ColumnType.Autonum);
        // a catalog numbered by a code has no date of its own; a document always has one
        var autonumDate = autonumColumn == null
            ? null
            : Table.AllColumns().FirstOrDefault(c => c.Type == ColumnType.Date);
        // the company splits the counter; at most one, checked at load (CheckCompanyColumn)
        var autonumCompany = autonumColumn == null
            ? null
            : Table.AllColumns().FirstOrDefault(c => c.Type == ColumnType.Company);

        List<(String Declared, String Output, String Into)> extras = [];
        if (autonumColumn != null)
        {
            if (autonumCompany == null)
                extras.Add(("[act] nvarchar(10)", "$action", "[act]"));
            extras.Add(("[num] nvarchar(64)", $"inserted.[{autonumColumn.Name}]", "[num]"));
            if (autonumDate != null)
                extras.Add(("[dt] date", $"inserted.[{autonumDate.Name}]", "[dt]"));
            if (autonumCompany != null)
            {
                var type = autonumCompany.SqlDataType();
                extras.Add(($"[comp] {type}", $"inserted.[{autonumCompany.Name}]", "[comp]"));
                extras.Add(($"[oldcomp] {type}", $"deleted.[{autonumCompany.Name}]", "[oldcomp]"));
            }
        }
        var rtableExtra = String.Concat(extras.Select(e => $", {e.Declared}"));
        var outputExtra = String.Concat(extras.Select(e => $", {e.Output}"));
        var outputIntoExtra = String.Concat(extras.Select(e => $", {e.Into}"));

        /* Issued after the merge, not inside it: there the insert list and its values are one
         * string, and wrapping one column in isnull() would have to break them apart for every
         * table.
         *
         * One condition, and both halves of it are load bearing. Only over an empty column, because
         * the user may write a number himself and correcting one is his right. And only on an
         * event - not 'whenever it is empty', which would number a document dated 2023 out of the
         * 2023 counter today: a row of authentic-looking last-year numbers, ordered by who opened
         * what. The price is that turning numbering on leaves old documents unnumbered and nothing
         * says so; that is a migration, where the order can be chosen. See ISSUES 3.8.
         *
         * The event is the moment the record first has all of the counter's key. Without a company
         * that is the insert. With one it is the save where the company goes from empty to filled -
         * the insert with a company among them, since 'deleted' is empty there: a draft saved with
         * no company would otherwise draw from the counter of no company, and repeat a number of the
         * company it is given later.
         */
        String Autonum()
        {
            if (autonumColumn == null)
                return String.Empty;
            var date = autonumDate != null ? "(select top(1) [dt] from @rtable)" : "getdate()";
            var issued = autonumCompany == null
                ? "[act] = N'INSERT'"
                : "[comp] is not null and [oldcomp] is null";
            List<String> declares = [$"declare @AutonumNo nvarchar(64), @AutonumDate date = {date};"];
            List<String> args = [$"@Autonum = N'{Endpoint.Declaration.Autonum}'", "@Date = @AutonumDate"];
            if (autonumCompany != null)
            {
                declares.Add($"declare @AutonumCompany {autonumCompany.SqlDataType()} = (select top(1) [comp] from @rtable);");
                args.Add("@Company = @AutonumCompany");
                // one prefix column or none is an answer; two is a guess, refused at load where '{p}' is written
                var catalog = autonumCompany.RefTableCheck.Storage;
                var prefixes = catalog.AllColumns(c => c.Type == ColumnType.Prefix).Take(2).ToList();
                if (prefixes.Count == 1)
                {
                    declares.Add($"declare @AutonumPrefix {prefixes[0].SqlDataType()} = (select [{prefixes[0].Name}] from {catalog.SqlTableName} where [Id] = @AutonumCompany);");
                    args.Add("@Prefix = @AutonumPrefix");
                }
            }
            args.Add("@Number = @AutonumNo output");
            return $"""
            -- autonum
            if exists(select 1 from @rtable where {issued} and isnull([num], N'') = N'')
            begin
                {String.Join($"{Environment.NewLine}    ", declares)}
                exec {TableMetadataDefaults.AutonumProcedureName()} {String.Join(", ", args)};
                update {Table.SqlTableName} set [{autonumColumn.Name}] = @AutonumNo where [Id] = @Id;
            end;

            """;
        }

        String buildSqlUpdateText()
        {
            // the key of THIS table: platformid for most, an account code for a chart of accounts
            var keyType = Table.KeyColumn.SqlDataType();

            /* A document with a list of operations is switched between them while it is edited, so its
             * operation is saved like any field; one implicit operation is the endpoint and never changes.
             */
            var switches = Endpoint.Declaration.OperationDeclarations.Count > 0;
            var updatedFields = Table.AllColumns(c => c.IsFieldUpdated() || switches && c.IsOperation)
                .Select(c => $"t.[{c.Name}] = s.[{c.Name}]");
            var insertedFields = Table.AllColumns(c => c.IsFieldInserted()).Select(c => $"[{c.Name}]").ToList();
            List<String> insertedValues = [.. insertedFields];
            /* A new record is created and modified by the same hand, so both stamps are written on
             * insert: left to its default, the modification stamp would name the system user.
             */
            if (Table.HasStamps)
            {
                insertedFields.AddRange([$"[{Constants.FieldNames.UserCreated}]", $"[{Constants.FieldNames.UtcDateCreated}]",
                    $"[{Constants.FieldNames.UserModified}]", $"[{Constants.FieldNames.UtcDateModified}]"]);
                insertedValues.AddRange(["@UserId", "getutcdate()", "@UserId", "getutcdate()"]);
            }

            var sb = new StringBuilder($"""
            set nocount on;
            set transaction isolation level read committed;
            set xact_abort on;
            {GateSql(SaveGate())}{RecordCheck(SentId)}

            declare @rtable table(Id {keyType}{rtableExtra});
            declare @Id {keyType};

            """);
            // STEP:1 - check row version
            sb.AppendLine(CheckRowVersion());
            sb.AppendLine(OwnerCheck(Table));
            sb.AppendLine(SentOperationCheck());

            // STEP:2 - merge main
            sb.AppendLine($"""
            -- merge main table
            merge {Table.SqlTableName} as t
            using @{Table.Model} as s
            on t.[Id] = s.[Id]
            when matched then update set
              {String.Join(",\n", updatedFields)}{ModifiedStamp("t.")}
            when not matched then insert
              ({String.Join(',', insertedFields)}) values
              ({String.Join(',', insertedValues)})
            output inserted.[Id]{outputExtra} into @rtable([Id]{outputIntoExtra});

            select @Id = [Id] from @rtable;

            """);

            // STEP:2a - the number, if this endpoint issues one
            sb.AppendLine(Autonum());

            // STEP:3 update details

            sb.AppendLine(MergeDetailsSql());

            sb.AppendLine(MergeTags());

            // STEP:4 return select

            sb.AppendLine(BuildLoadPlainSqlText(LoadRows.Stored, birth: null, head: String.Empty));

            return sb.ToString();
        }

        var sqlText = buildSqlUpdateText();

        var item = data.Get<ExpandoObject>(Table.Model);
        var tableBuilder = new DataTableBuilder(Table, PlatformId);
        var dtable = tableBuilder.BuildDataTable(item);

        List<(String name, String typeName, DataTable table)> detailsTables = [];

        if (Table.Details.Count > 0)
        {
            foreach (var t in Table.Details)
            {
                var detailsTableBuilder = new DataTableBuilder(t.Value, PlatformId);
                /* The parameter is named after the collection it carries, not after the kind:
                 * 'Stock' declared in both Rows and Links is legal and would give two @Stock
                 * in one batch. The table type is one either way - one table, one shape.
                 */
                IEnumerable<String> sources = t.Value.Kinds.Count > 0
                    ? t.Value.Kinds.Keys.Select(t.Value.KindCollectionName)
                    : [t.Key];
                foreach (var name in sources)
                {
                    var rows = item?.Get<List<Object>>(name);
                    var dt = detailsTableBuilder.BuildDataTable(rows);
                    detailsTables.Add(($"@{name}", t.Value.SqlTableTypeName, dt));
                }
            }
        }

        var dm = await _dbContext.LoadModelSqlAsync(DataSource, sqlText, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddStructured($"@{Table.Model}", Table.SqlTableTypeName, dtable);
            foreach (var (name, typeName, table) in detailsTables)
                dbprms.AddStructured(name, typeName, table);
            if (Table.HasTags)
                dbprms.AddStructured(TagsParam, Constants.SqlNames.IdTableType,
                    DataTableBuilder.BuildIdTable(item?.Get<List<Object>>(Constants.FieldNames.Tags), PlatformId));
            // the load after the save reads them too (BuildLoadPlainSqlText); a save has no url to take them from
            foreach (var (key, _) in QueryInitials())
                dbprms.AddString($"@Query{key}", null);
        });

        return dm.Root;
    }
}

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
    String BuildLoadPlainSqlText()
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
                    "today" => $"[{Table.Model}.{key}!!Utc] = a2meta.fn_getUtcDate()",
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

            """);
        sb.AppendLine();

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
        sb.AppendLine($"from {Table.SqlTableName} a where a.Id = @Id;");


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
                    from {dt.SqlTableName} d where d.[{dt.MasterField}] = @Id{filter}
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

        var refMap = new RefMapBuilder(Endpoint, isPlain: true, hasDefaults: IsNewModel());

        // STEP 3: map recordsets

        refMap.WriteRefMap(sb);

        // without the 'All' row: a record picks a value, and 'all of them' is not one
        foreach (var en in ReferencedSets(withDetails: true))
        {
            sb.AppendLine();
            sb.AppendLine(ValuesRecordset(en, withAll: false));
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

        var sqlQuery = BuildLoadPlainSqlText();

        return await _dbContext.LoadModelSqlAsync(_descr.DataSource, sqlQuery, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddString("@Id", _descr.PlatformUrl.Id);
            foreach (var (key, param) in QueryInitials())
                dbprms.AddString($"@Query{key}", QueryValue(param));
        });
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

        String MergeDetails()
        {
            if (Table.Details == null || Table.Details.Count == 0)
                return String.Empty;
            var sb = new StringBuilder("-- merge details");
            sb.AppendLine();

            Boolean updateablePredicate(TableColumn c)
                => c.Type != ColumnType.Master && c.Type != ColumnType.RowKind && c.Type != ColumnType.Id;

            String mergeOneDetails(TableMetadata detailsTable, String key)
            {
                var updateFields = detailsTable.AllColumns(updateablePredicate);

                return $"""
				merge {detailsTable.SqlTableName} as t
				using @{key} as s
				on t.[Id]  = s.[Id]
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
				on t.Id = s.Id
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


        /* Tags are not a detail: no fields of their own, no RowNo, nothing to update - a row either
         * is in the set or is not. So the merge has no 'when matched' arm, and what is deleted is
         * bounded by the master exactly as the details merges bound theirs.
         */
        String MergeTags()
        {
            if (!Table.HasTags)
                return String.Empty;
            return $"""
            -- merge tags
            merge {TableMetadataDefaults.TagEntriesTableName(Table.Model)} as t
            using @{Constants.FieldNames.Tags} as s
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

        List<(String Declared, String Output, String Into)> extras = [];
        if (autonumColumn != null)
        {
            extras.Add(("[act] nvarchar(10)", "$action", "[act]"));
            extras.Add(("[num] nvarchar(64)", $"inserted.[{autonumColumn.Name}]", "[num]"));
            if (autonumDate != null)
                extras.Add(("[dt] date", $"inserted.[{autonumDate.Name}]", "[dt]"));
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
         * insert - not 'whenever it is empty', which would number a document dated 2023 out of the
         * 2023 counter today: a row of authentic-looking last-year numbers, ordered by who opened
         * what. The price is that turning numbering on leaves old documents unnumbered and nothing
         * says so; that is a migration, where the order can be chosen. See ISSUES 3.8.
         */
        String Autonum()
        {
            if (autonumColumn == null)
                return String.Empty;
            var date = autonumDate != null ? "(select top(1) [dt] from @rtable)" : "getdate()";
            return $"""
            -- autonum
            if exists(select 1 from @rtable where [act] = N'INSERT' and isnull([num], N'') = N'')
            begin
                declare @AutonumNo nvarchar(64), @AutonumDate date = {date};
                exec {TableMetadataDefaults.AutonumProcedureName()} @Autonum = N'{Endpoint.Declaration.Autonum}',
                    @Date = @AutonumDate, @Number = @AutonumNo output;
                update {Table.SqlTableName} set [{autonumColumn.Name}] = @AutonumNo where [Id] = @Id;
            end;

            """;
        }

        String buildSqlUpdateText()
        {
            // the key of THIS table: platformid for most, an account code for a chart of accounts
            var keyType = Table.KeyColumn.SqlDataType();

            var updatedFields = Table.AllColumns(c => c.IsFieldUpdated()).Select(c => $"t.[{c.Name}] = s.[{c.Name}]");
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

            declare @rtable table(Id {keyType}{rtableExtra});
            declare @Id {keyType};

            """);
            // STEP:1 - check row version
            sb.AppendLine(CheckRowVersion());

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

            sb.AppendLine(MergeDetails());

            sb.AppendLine(MergeTags());

            // STEP:4 return select

            sb.AppendLine(BuildLoadPlainSqlText());

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
                dbprms.AddStructured($"@{Constants.FieldNames.Tags}", Constants.SqlNames.IdTableType,
                    DataTableBuilder.BuildIdTable(item?.Get<List<Object>>(Constants.FieldNames.Tags), PlatformId));
            // the load after the save reads them too (BuildLoadPlainSqlText); a save has no url to take them from
            foreach (var (key, _) in QueryInitials())
                dbprms.AddString($"@Query{key}", null);
        });

        return dm.Root;
    }
}

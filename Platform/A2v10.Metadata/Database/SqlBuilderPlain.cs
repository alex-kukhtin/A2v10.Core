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
    String BuildLoadPlainSqlText()
    {
        var allColumns = Table.AllColumns().ToList();
        // var refs = allColumns.AllRefs().ToList();

        IEnumerable<String> plainSqlFields(String alias)
        {
            static Boolean includeColumn(TableColumn col)
                => col.Type != ColumnType.Void;
            return Table.AllColumns(includeColumn).Select(col => col.SqlModelColumnName(alias, t => t.TypeName));
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
            select [!TTag!Array] = null, [Id!!Id] = t.Id, [Name!!Name] = t.[Name], [Color] = t.[Color],
              [!{Table.TypeName}.{Constants.FieldNames.Tags}!ParentId] = e.[Owner]
            from {Table.SqlSchema}.[{Table.Model}$TagEntries] e
              inner join cat.[$Tags] t on t.[Id] = e.[Tag]
            where e.[Owner] = @Id and t.[For] = N'{Table.Model}';

            select [{Constants.FieldNames.Tags}!TTag!Array] = null, [Id!!Id] = t.Id,
              [Name!!Name] = t.[Name], [Color] = t.[Color]
            from cat.[$Tags] t where t.[For] = N'{Table.Model}'
            order by t.[Id];
            """;

        String? generateDefaults()
        {
            var org = Endpoint.Declaration;
            if (!IsNewModel())
                return null;
            var initValues = org.InitialValues;
            var docOp = Endpoint.DocumentOperation();
            if (docOp != null)
                initValues = new Dictionary<string, InitialMetadata>(initValues)
                {
                    ["Operation"] = new InitialMetadata(
                        Source: InitialSource.Context,
                        Value: "$operation$"
                    )
                };
            
            if (initValues.Count == 0)
                return null;

            String getDefaultProfile(String key)
            {
                var column = Table.Columns.FirstOrDefault(c => c.Name == key)
                    ?? throw new InvalidOperationException($"Column {key} not found in {Table.SqlTableName}");
                return $"[{Table.Model}.{key}!{column.RefTableCheck.Storage.TypeName}!RefId] = @Init{key}";
            }

            String getDefaultContext(String key, String value)
            {
                return value switch
                {
                    "today" => $"[{Table.Model}.{key}!!Utc] = a2meta.fn_getUtcDate()",
                    "$operation$" => $"[{Table.Model}.{key}!TOperation!RefId] = N'{docOp}'",
                    _ => throw new InvalidOperationException($"Invalid initial context value '{value}'")
                };
            }

            var sb = new StringBuilder("select [!$Defaults!] = null, ");

            sb.AppendJoin(", ", initValues.Select(p =>
                p.Value.Source switch
                {
                    InitialSource.Profile => getDefaultProfile(p.Key),
                    InitialSource.Context => getDefaultContext(p.Key, p.Value.Value),
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

        // STEP 1: main recordset
        sb.AppendLine("-- main recordset");

        sb.Append($"""
            select [{Table.Model}!{Table.TypeName}!Object] = null, {String.Join(", ", plainSqlFields("a"))}
            """);
        // slots the object carries beyond its own columns: one per collection, and the tags array
        List<String> arraySlots = [.. Table.Details.Select(mainDetailsFields)];
        if (Table.HasTags)
            arraySlots.Add($"[{Constants.FieldNames.Tags}!TTag!Array] = null");

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

                var detailsFields = dt.Columns.Where(c => includeDetailsColumn(c)).Select(col => col.SqlModelColumnName("d", t => t.TypeName)).ToList();

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
                      [!{Table.TypeName}.{parent}!ParentId] = d.[Owner]
                    from {dt.SqlTableName} d where d.[Owner] = @Id{filter}
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

        var refMap = new RefMapBuilder(Endpoint, isPlain: true, hasDefaults: IsNewModel());

        // STEP 3: map recordsets

        refMap.WriteRefMap(sb);

        var defs = generateDefaults();
        if (defs != null) {
            sb.AppendLine();
            sb.AppendLine(defs);
        }

        // STEP 5: system recorset
        if (Table.IsDocument)
        {
            sb.AppendLine();
            sb.AppendLine("-- system recordset");
            sb.Append($"""
                select [!$System!] = null, [!!ReadOnly] = a.Done
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
                => c.Type != ColumnType.Owner && c.Type != ColumnType.RowKind && c.Type != ColumnType.Id;

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
				    ([Owner], {String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
				    (@Id, {String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
				when not matched by source and t.[Owner] = @Id then delete;
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
                    $"select [__Kind__] = N'{k}', * from @{detailsTable.KindCollectionName(k)}"
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
					([Owner], [{kindField.Name}], {String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
					(@Id, s.[__Kind__], {String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
				when not matched by source and t.[Owner] = @Id and t.[{kindField.Name}] in ({declaredKinds}) then delete;
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
         * bounded by the owner exactly as the details merges bound theirs.
         */
        String MergeTags()
        {
            if (!Table.HasTags)
                return String.Empty;
            return $"""
            -- merge tags
            merge {Table.SqlSchema}.[{Table.Model}$TagEntries] as t
            using @{Constants.FieldNames.Tags} as s
            on t.[Owner] = @Id and t.[Tag] = s.[Id]
            when not matched then insert ([Owner], [Tag]) values (@Id, s.[Id])
            when not matched by source and t.[Owner] = @Id then delete;
            """;
        }

        String buildSqlUpdateText()
        {

            var updatedFields = Table.AllColumns(c => c.IsFieldUpdated()).Select(c => $"t.[{c.Name}] = s.[{c.Name}]");
            var insertedFields = Table.AllColumns(c => c.IsFieldInserted()).Select(c => $"[{c.Name}]");

            var sb = new StringBuilder("""
            set nocount on;
            set transaction isolation level read committed;
            set xact_abort on;
            
            declare @rtable table(Id platformid);
            declare @Id platformid;                        

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
              {String.Join(",\n", updatedFields)}
            when not matched then insert
              ({String.Join(',', insertedFields)}) values
              ({String.Join(',', insertedFields)}) 
            output inserted.[Id] into @rtable([Id]);
            
            select @Id = [Id] from @rtable;
            
            """);

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
        });

        return dm.Root;
    }
}

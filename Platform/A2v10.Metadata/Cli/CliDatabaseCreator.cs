// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;

namespace A2v10.Metadata.Cli;

public class CliDatabaseCreator()
{
    public const String SQL_DIVIDER = "------------------------------------------------";
    private static readonly String NL = Environment.NewLine;
    private static readonly String INDENT = "       ";
    internal String CreateTable(TableMetadata table)
    {

        String createField(TableColumn column)
        {
            const String NOT_NULL = " not null";

            var constraint = String.Empty;
            if (column.Type == ColumnType.Id)
                constraint = $"{NL}{INDENT}constraint DF_{table.Table}_{column.Name} default(next value for {table.SqlSequenceName})";
            else if (column.DeployDefault() is String dflt)
                constraint = $"{NL}{INDENT}constraint DF_{table.Table}_{column.Name} default({dflt})";

            // nullability comes from DeployNullable - the same source the seed uses
            var nullable = column.DeployNullable() ? null : NOT_NULL;
            return $"[{column.Name}] {column.SqlDataType()}{nullable}{constraint}";
        }

        String createSequence()
        {
            return $"""
            if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA = N'{table.SqlSchema}' and SEQUENCE_NAME = N'SQ_{table.Table}')
            	create sequence {table.SqlSequenceName} as bigint start with 1000 increment by 1;
            """;
        }

        var fields = table.AllColumns().Select(createField);

        return $"""
        {SQL_DIVIDER}
        {createSequence()}

        if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'{table.SqlSchema}' and TABLE_NAME=N'{table.Table}')
        create table {table.SqlTableName}
        (
            {String.Join($",{NL}    ", fields)},
            constraint PK_{table.Table} primary key (Id)
        );
        """;
    }

    // generic, so it is not gated on the trait that happens to be its first consumer
    public static String CreateIdTableType() => $"""
        {SQL_DIVIDER}
        drop type if exists {Constants.SqlNames.IdTableType};
        create type {Constants.SqlNames.IdTableType} as table
        (
            [{Constants.FieldNames.Id}] platformid
        );
        """;

    public static String CreateTableType(TableMetadata table)
    {
        static String createField(TableColumn column)
        {
            return $"[{column.Name}] {column.SqlDataType(true)}";
        }

        var fields = table.AllColumns(TableColumnPredicates.IsSentColumn).Select(createField);

        return $"""
        {SQL_DIVIDER}
        drop type if exists {table.SqlTableTypeName};
        create type {table.SqlTableTypeName} as table
        (
            {String.Join($",{NL}    ", fields)}
        );
        """;
    }

    public static String CreateForeignKeys(TableMetadata table, TableMetadata? master = null)
    {
        //const String check = "nocheck"; // TODO: ????

        /* One shape for every foreign key here - only the name and the target differ. The
         * truncation used to sit on the last branch alone, so a long name was a SQL error on
         * the other three; it belongs to the shape, not to one case.
         */
        String Constraint(String name, TableColumn column, String targetTableName)
        {
            if (name.Length > 128)
                name = name[0..127];
            return $"""
            if not exists(select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where TABLE_SCHEMA = N'{table.SqlSchema}' and TABLE_NAME = N'{table.Table}' and CONSTRAINT_NAME = N'{name}')
                alter table {table.SqlTableName} add
                    constraint {name} foreign key ([{column.Name}]) references {targetTableName}([Id]);
            """;
            //alter table {table.SqlTableName} {check} constraint {name};
        }

        String createReference(TableColumn column)
        {
            if (column.Type == ColumnType.Master)
            {
                /* The target is not on the column: it is the table this one hangs under, and only
                 * the deploy walk knows it (SqlDbGenerator.DeployTables). A master column reaching
                 * here without it means the walk yielded this table as a top-level one.
                 */
                if (master == null)
                    throw new InvalidOperationException($"The master table for {table.SqlTableName} is null");
                return Constraint($"FK_{table.Table}_{column.Name}_{master.Table}", column, master.SqlTableName);
            }
            else if (column.Type == ColumnType.Operation)
            {
                var ops = TableMetadataDefaults.OperationsTable();
                return Constraint($"FK_{table.Table}_{column.Name}_{ops.Table}", column, ops.SqlTableName);
            }
            else if (table.IsTagEntries)
            {
                /* The tags catalog is platform-owned and sits at a fixed address - the same case
                 * as Operations above, and for the same reason there is nothing to resolve: a
                 * tag entries table is built on the fly (CreateTagEntriesTable) and never goes
                 * through reference linking, so its RefTable is null.
                 */
                var tags = TableMetadataDefaults.TagsTable();
                return Constraint($"FK_{table.Table}_{column.Name}_{tags.Table}", column, tags.SqlTableName);
            }
            var refStorage = column.RefTableCheck.Storage;
            return Constraint($"FK_{table.Table}_{column.Name}_{refStorage.Table}", column, refStorage.SqlTableName);
        }
        var refs = table.AllColumns().Where(c => c.IsRef)
            .Select(rc => createReference(rc));
        // a login is not an endpoint: the target is fixed, as Operations is above
        var stamps = table.AllColumns(c => c.Type is ColumnType.StampUser or ColumnType.StampUserNull)
            .Select(c => Constraint($"FK_{table.Table}_{c.Name}_Users", c, "a2security.Users"));
        var res = String.Join(Environment.NewLine, refs.Concat(stamps));
        if (String.IsNullOrEmpty(res.Trim()))
            return String.Empty;
        return $"""
            {SQL_DIVIDER}
            {res}
            """;
    }

    /* Created after the table and its rows, so a unique index meets the data that is already there:
     * duplicates fail the deploy loudly instead of being carried forward under a promise the
     * database does not keep.
     */
    public static String CreateIndexes(TableMetadata table)
    {
        if (table.Indexes.Count == 0)
            return String.Empty;

        String createIndex(TableIndex index)
        {
            var name = index.Name(table);
            var columns = String.Join(", ", index.Columns.Select(c => $"[{c}]"));
            return $"""
            if not exists(select * from sys.indexes where object_id = object_id(N'{table.SqlTableName}') and name = N'{name}')
                create {(index.Unique ? "unique " : "")}index {name} on {table.SqlTableName} ({columns});
            """;
        }

        return $"""
        {SQL_DIVIDER}
        {String.Join(NL, table.Indexes.Select(createIndex))}
        """;
    }
}

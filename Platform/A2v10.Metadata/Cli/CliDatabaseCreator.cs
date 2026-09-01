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
            else if (column.HasDefaultBit)
                constraint = $"{NL}{INDENT}constraint DF_{table.Table}_{column.Name} default(0)";

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

        var fields = table.AllColumns().Select(createField);

        return $"""
        {SQL_DIVIDER}
        drop type if exists {table.SqlTableTypeName};
        create type {table.SqlTableTypeName} as table
        (
            {String.Join($",{NL}    ", fields)}
        );
        """;
    }

    public static String CreateForeignKeys(TableMetadata table, TableMetadata? owner = null)
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
            if (column.Type == ColumnType.Owner)
            {
                if (owner == null)
                    throw new InvalidOperationException("Owern is null");
                return Constraint($"FK_{table.Table}_{column.Name}_{owner.Table}", column, owner.SqlTableName);
            }
            else if (column.Type == ColumnType.Operation)
                return Constraint($"FK_{table.Table}_{column.Name}_Operations", column, "op.[Operations]");
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
        var res = String.Join(Environment.NewLine, refs);
        if (String.IsNullOrEmpty(res.Trim()))
            return String.Empty;
        return $"""
            {SQL_DIVIDER}
            {res}
            """;
    }

    internal static String MergeOperations()
    {
        return """
        merge op.Operations as t
        using @Operations as s
        on t.Id = s.Id
        when matched then update set
            t.[Name] = s.[Name],
            t.[Url] = s.[Url],
            t.[Category] = s.[Category]
        when not matched then insert
            (Id, [Name], [Url], [Category]) values
            (s.Id, s.[Name], s.[Url], [Category]);
        """;
    }

    internal static DataTable CreateOperationTable(IEnumerable<OperationMetadata> ops)
    {
        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(String)).MaxLength = 64;
        dt.Columns.Add("Name", typeof(String)).MaxLength = 255;
        dt.Columns.Add("Url", typeof(String)).MaxLength = 255;
        dt.Columns.Add("Category", typeof(String)).MaxLength = 32;

        foreach (var op in ops)
        {
            var dr = dt.NewRow();
            dr["Id"] = op.Id;
            dr["Name"] = op.Name ?? op.Id;
            dr["Url"] = $"/operation/{op.Id.ToLowerInvariant()}/edit";
            dr["Category"] = op.Category;
            dt.Rows.Add(dr);
        }
        return dt;
    }

    internal static String CreateOperations(IEnumerable<OperationMetadata> ops)
    {
        if (!ops.Any())
            return String.Empty;

        return $"""
        if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'op' and TABLE_NAME=N'Operations')
        create table op.[Operations] 
        (
            [Id] nvarchar(64) not null
                constraint PK_Operations primary key,
            [Void] bit not null
                constraint DF_Operations_Void default(0),
            [Name] nvarchar(255),
            [Category] nvarchar(255),
            [Url] nvarchar(255)
        );
        """;
    }

}

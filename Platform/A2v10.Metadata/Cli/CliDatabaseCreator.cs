// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;

namespace A2v10.Metadata.Cli;

public class CliDatabaseCreator()
{
    private const String SQL_DIVIDER = "------------------------------------------------";
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

            // nullable рахує DeployNullable — те саме джерело, що й seed
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
        String createReference(TableColumn column)
        {
            if (column.Type == ColumnType.Owner)
            {
                if (owner == null)
                    throw new InvalidOperationException("Owern is null");

                var opConstraintName = $"FK_{table.Table}_{column.Name}_{owner.Table}";

                return $"""
                if not exists(select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where TABLE_SCHEMA = N'{table.SqlSchema}' and TABLE_NAME = N'{table.Table}' and CONSTRAINT_NAME = N'{opConstraintName}')
                    alter table {table.SqlTableName} add 
                        constraint {opConstraintName} foreign key ([{column.Name}]) references {owner.SqlTableName}([Id]);
                """;
            }
            else if (column.Type == ColumnType.Operation)
            {
                var opConstraintName = $"FK_{table.Table}_{column.Name}_Operations";

                return $"""
                if not exists(select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where TABLE_SCHEMA = N'{table.SqlSchema}' and TABLE_NAME = N'{table.Table}' and CONSTRAINT_NAME = N'{opConstraintName}')
                    alter table {table.SqlTableName} add 
                        constraint {opConstraintName} foreign key ([{column.Name}]) references op.[Operations]([Id]);
                """;
                //alter table {table.SqlTableName} {check} constraint {opConstraintName};
            }
            else if (column.Type == ColumnType.Enum)
            {
                var opConstraintName = $"FK_{table.Table}_{column.Name}_{column.RefTableCheck.Table}";

                return $"""
                if not exists(select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where TABLE_SCHEMA = N'{table.SqlSchema}' and TABLE_NAME = N'{table.Table}' and CONSTRAINT_NAME = N'{opConstraintName}')
                    alter table {table.SqlTableName} add 
                        constraint {opConstraintName} foreign key ([{column.Name}]) references {column.RefTableCheck.SqlTableName}([Id]);
                """;
                //alter table {table.SqlTableName} {check} constraint {opConstraintName};
            }

            var constraintName = $"FK_{table.Table}_{column.Name}_{column.RefTableCheck.Table}";
            if (constraintName.Length > 128)
                constraintName = constraintName[0..127];
            return $"""
            if not exists(select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where TABLE_SCHEMA = N'{table.SqlSchema}' and TABLE_NAME = N'{table.Table}' and CONSTRAINT_NAME = N'{constraintName}')
                alter table {table.SqlTableName} add 
                    constraint {constraintName} foreign key ([{column.Name}]) references {column.RefTableCheck.SqlTableName}([Id]);
            """;
            //alter table {table.SqlTableName} {check} constraint {constraintName};
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

    internal static String CreateEnum(EnumMetadata enm)
    {
        return $"""
        {SQL_DIVIDER}
        if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'enm' and TABLE_NAME=N'{enm.Name}')
        create table enm.[{enm.Name}]
        (
            [Id] nvarchar(16) not null
                constraint PK_{enm.Name} primary key,
            [Name] nvarchar(255),
            [Order] int not null,
            Inactive bit not null
                constraint DF_{enm.Name}_Inactive default(0)
        );
        """;
    }

    internal static DataTable CreateEnumTable(EnumMetadata enm)
    {
        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(String)).MaxLength = 16;
        dt.Columns.Add("Name", typeof(String)).MaxLength = 255;
        dt.Columns.Add("Order", typeof(Int32));
        dt.Columns.Add("Inactive", typeof(Boolean));

        // add "All"
        var ar = dt.NewRow();
        ar["Id"] = "";
        ar["Name"] = $"@[{enm.Name}.All]";
        ar["Order"] = -1;
        ar["Inactive"] = false;
        dt.Rows.Add(ar);


        foreach (var val in enm.Values)
        {
            var dr = dt.NewRow();
            dr["Id"] = val.Id;
            dr["Name"] = val.Name ?? val.Id;
            dr["Order"] = val.Order;
            dr["Inactive"] = val.Inactive == true;
            dt.Rows.Add(dr);
        }
        return dt;
    }

    internal static String MergeEnums(EnumMetadata enm)
    {
        return $"""
        merge enm.[{enm.Name}] as t
        using @Enums as s
        on t.Id = s.Id
        when matched then update set
            t.[Name] = s.[Name],
            t.[Order] = s.[Order],
            t.[Inactive] = s.[Inactive]
        when not matched then insert
            (Id, [Name], [Order], [Inactive]) values
            (s.Id, s.[Name], s.[Order], [Inactive]);
        """;
    }
}

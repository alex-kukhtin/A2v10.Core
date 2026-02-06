// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;

namespace A2v10.Metadata;

internal class DatabaseCreator(AppMetadata _meta)
{
    internal String CreateTable(TableMetadata table, Boolean skipAlter = false)
    {

        var multPrimaryKeys = table.PrimaryKeys.Count() > 1;

        String createField(TableColumn column)
        {
            const String NOT_NULL = " not null";

            String? nullable = null;
            var constraint = String.Empty;
            if (column.Role.HasFlag(TableColumnRole.PrimaryKey))
            {
                nullable = NOT_NULL;
                var colDataType = column.DataType;
                if (colDataType == ColumnDataType.Id)
                    colDataType = _meta.IdDataType;
                if (!multPrimaryKeys && table.HasSequence) 
                {
                    var defKey = colDataType switch
                    {
                        ColumnDataType.Id => $"next value for {table.Schema}.[SQ_{table.Name}]",
                        ColumnDataType.Uniqueidentifier => "newsequentialid()",
                        ColumnDataType.Int or ColumnDataType.BigInt => $"next value for {table.Schema}.[SQ_{table.Name}]",
                        ColumnDataType.Date or ColumnDataType.DateTime => null,
                        ColumnDataType.String => null,   
                        ColumnDataType.Reference => null,
                        ColumnDataType.Enum => null,
                        _ => throw new InvalidOperationException($"Defaults for {column.DataType} is not supported")
                    };
                    if (defKey != null)
                        constraint = $"\r\n       constraint DF_{table.Name}_{column.Name} default({defKey})";
                }
            }
            else if (column.HasDefaultBit)
            {
                nullable = NOT_NULL;
                constraint = $"\r\n       constraint DF_{table.Name}_{column.Name} default(0)";
            }
            return $"[{column.Name}] {column.SqlDataType(_meta.IdDataType)}{nullable}{constraint}";
        }

        String alterCreateField(TableColumn column)
        {
            return $"alter table {table.SqlTableName} add {createField(column)}";
        }

        String createSequence()
        {
            if (!table.HasSequence)
                return String.Empty;
            if (_meta.IdDataType != ColumnDataType.Int && _meta.IdDataType != ColumnDataType.BigInt)
                return String.Empty;
            return $"""
            ------------------------------------------------
            if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA = N'{table.Schema}' and SEQUENCE_NAME = N'SQ_{table.Name}')
            	create sequence {table.Schema}.[SQ_{table.Name}] as {_meta.IdDataType.ToString().ToLowerInvariant()} start with 1000 increment by 1;
            """;
        }

        var fields = table.Columns.Select(createField);

        var primaryKeys = table.PrimaryKeys.Select(c => $"[{c.Name}]");

        var alterFields = table.Columns
                .Where(c => String.IsNullOrEmpty(c.DbName) && !c.DbDataType.HasValue)
                .Select(alterCreateField);

        if (table.HasDbTable && !skipAlter)
            return String.Join(Environment.NewLine, alterFields);

        return $"""
        {createSequence()}
        ------------------------------------------------
        if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'{table.Schema}' and TABLE_NAME=N'{table.Name}')
        create table {table.SqlTableName}
        (
            {String.Join(",\r\n    ", fields)},
            constraint PK_{table.Name} primary key ({String.Join(',', primaryKeys)})
        );
        """;
    }

    internal String CreateTableType(TableMetadata table)
    {
        var idDataType = _meta.IdDataType;

        String createField(TableColumn column)
        {
            return $"[{column.Name}] {column.SqlDataType(idDataType, true)}";
        }

        var fields = table.Columns.Select(createField);

        return $"""
        ------------------------------------------------
        drop type if exists {table.Schema}.[{table.Name}.TableType];
        create type {table.Schema}.[{table.Name}.TableType] as table
        (
            {String.Join(",\r\n    ", fields)}
        );
        """;
    }

    internal String CreateForeignKeys(TableMetadata table)
    {
        const String check = "nocheck"; // TODO: ????
        String createReference(TableColumn column)
        {
            if (column.DataType == ColumnDataType.Operation)
            {
                var opConstraintName = $"FK_{table.Name}_{column.Name}_Operations";

                return $"""
                if not exists(select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where TABLE_SCHEMA = N'{table.Schema}' and TABLE_NAME = N'{table.Name}' and CONSTRAINT_NAME = N'{opConstraintName}')
                    alter table {table.SqlTableName} add 
                        constraint {opConstraintName} foreign key ([{column.Name}]) references op.[Operations]([Id]);
                alter table {table.SqlTableName} {check} constraint {opConstraintName};
                """;
            }
            else if (column.DataType == ColumnDataType.Enum)
            {
                var opConstraintName = $"FK_{table.Name}_{column.Name}_{column.Reference.RefTable}";

                return $"""
                if not exists(select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where TABLE_SCHEMA = N'{table.Schema}' and TABLE_NAME = N'{table.Name}' and CONSTRAINT_NAME = N'{opConstraintName}')
                    alter table {table.SqlTableName} add 
                        constraint {opConstraintName} foreign key ([{column.Name}]) references {column.Reference.SqlTableName}([Id]);
                alter table {table.SqlTableName} {check} constraint {opConstraintName};
                """;
            }

            var refs = column.Reference ??
                    throw new InvalidOperationException("Reference is null");

            var refTable = _meta.Tables.FirstOrDefault(x => x.Schema == refs.RefSchema && x.Name == refs.RefTable)
                ?? throw new InvalidOperationException($"Reference table {refs.RefSchema}.{refs.RefTable} not found");
            var refTablePk = refTable.PrimaryKeys;
            if (refTablePk.Count() > 1)
            {
                throw new InvalidOperationException("TODO: Implement multi-column foreign key");    
            }
            var refTablePkName = refTablePk.First().Name;

            var constraintName = $"FK_{table.Name}_{column.Name}_{refs.RefTable}";
            if (constraintName.Length > 128)
                constraintName = constraintName[0..127];
            return $"""
            if not exists(select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where TABLE_SCHEMA = N'{table.Schema}' and TABLE_NAME = N'{table.Name}' and CONSTRAINT_NAME = N'{constraintName}')
                alter table {table.SqlTableName} add 
                    constraint {constraintName} foreign key ([{column.Name}]) references {refs.RefSchema}.[{refs.RefTable}]([{refTablePkName}]);
            alter table {table.SqlTableName} {check} constraint {constraintName};
            """;
        }
        var refs = table.Columns.Where(c => c.IsReference || c.DataType == ColumnDataType.Operation || c.DataType == ColumnDataType.Enum)
            .Select(rc => createReference(rc));
        var res = String.Join(Environment.NewLine, refs);
        if (String.IsNullOrEmpty(res.Trim()))
            return String.Empty;
        return $"""
            ------------------------------------------------
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
        ------------------------------------------------
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

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

namespace A2v10.Metadata;

internal class DatabaseCreator(AppMetadata _meta)
{
    internal String CreateTable(TableMetadata table)
    {
        String createField(TableColumn column)
        {
            const String NOT_NULL = " not null";

            String? nullable = null;
            var constraint = String.Empty;
            if (column.Name == _meta.IdField)
            {
                nullable = NOT_NULL;
                var defKey = _meta.IdDataType switch
                {
                    ColumnDataType.Uniqueidentifier => "newid()",
                    ColumnDataType.Int or ColumnDataType.BigInt => $"next value for {table.Schema}.SQ_{table.Name}",
                    _ => throw new InvalidOperationException($"Defaults for {_meta.IdDataType} is not supported")
                };
                constraint = $"\n       constraint DF_{table.Name}_{column.Name} default({defKey})";
            }
            else if (_meta.HasConstraint(column.Name))
            {
                nullable = NOT_NULL;
                constraint = $"\n       constraint DF_{table.Name}_{column.Name} default(0)";
            }
            return $"[{column.Name}] {column.SqlDataType(_meta.IdDataType)}{nullable}{constraint}";
        }

        String createSequence()
        {
            if (_meta.IdDataType != ColumnDataType.Int && _meta.IdDataType != ColumnDataType.BigInt)
                return String.Empty;
            return $"""
            if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA = N'{table.Schema}' and SEQUENCE_NAME = N'SQ_{table.Name}')
            	create sequence {table.Schema}.SQ_{table.Name} as {_meta.IdDataType.ToString().ToLowerInvariant()} start with 1000 increment by 1;

            """;
        }

        var fields = table.Columns.Select(c => createField(c));

        return $"""
        {createSequence()}

        if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'{table.Schema}' and TABLE_NAME=N'{table.Name}')
        create table {table.Schema}.[{table.Name}]
        (
            {String.Join(",\n    ", fields)},
            constraint PK_{table.Name} primary key ([{_meta.IdField}])
        );
        """;
    }

    internal String CreateTableType(TableMetadata table)
    {
        var idDataType = _meta.IdDataType;

        String createField(TableColumn column)
        {
            return $"[{column.Name}] {column.SqlDataType(idDataType)}";
        }

        var fields = table.Columns.Select(c => createField(c));

        return $"""
        drop type if exists {table.Schema}.[{table.Name}.TableType];
        create type {table.Schema}.[{table.Name}.TableType] as table
        (
            {String.Join(",\n    ", fields)}
        );
        go        
        """;
    }

    internal String CreateForeignKeys(TableMetadata table)
    {
        String createReference(TableColumn column)
        {
            var refs = column.Reference ??
                throw new InvalidOperationException("Reference is null");
            var constraintName = $"FK_{table.Name}_{column.Name}_{refs.RefTable}";
            return $"""
            if not exists(select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where TABLE_SCHEMA = N'{table.Schema}' and TABLE_NAME = N'{table.Name}' and CONSTRAINT_NAME = N'{constraintName}')
                alter table {table.Schema}.[{table.Name}] add 
                    constraint {constraintName} foreign key ([{column.Name}]) references {refs.RefSchema}.[{refs.RefTable}]({_meta.IdField});
            """;
        }
        var refs = table.Columns.Where(c => c.IsReference).Select(rc => createReference(rc));
        return String.Join('\n', refs);
    }
}

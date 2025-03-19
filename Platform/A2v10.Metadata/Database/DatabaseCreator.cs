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

            var nullable = column.Name == _meta.IdField ? NOT_NULL : String.Empty;
            var constraint = String.Empty;
            if (_meta.HasConstraint(column.Name))
            {
                nullable = NOT_NULL;
                constraint = $"\n       constraint DF_{table.Name}_{column.Name} default(0)";
            }
            return $"[{column.Name}] {column.SqlDataType(_meta.IdDataType)}{nullable}{constraint}";
        }

        var fields = table.Columns.Select(c => createField(c));

        return $"""
        if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'{table.Name}' and TABLE_NAME=N'{table.Schema}')
        create table {table.Schema}.[{table.Name}]
        (
            {String.Join(",\n    ", fields)},
            constraint PK_{table.Name} primary key ([{_meta.IdField}])
        );
        go        
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
            var constraintName = $"FK_{table.Name}_{column.Name}_{refs.RefColumn}";
            return $"""
            if not exists(select * from INFORMATION_SCHEMA.  where  and N'{constraintName}')
                alter table {table.Schema}.[{table.Name}] add 
                    constraint {constraintName} foreign key ([{column.Name}]) references {refs.RefSchema}.[{refs.RefTable}]({_meta.IdField});
            """;
        }
        var refs = table.Columns.Where(c => c.IsReference).Select(c => createReference(c));
        return String.Join('\n', refs);
    }
}

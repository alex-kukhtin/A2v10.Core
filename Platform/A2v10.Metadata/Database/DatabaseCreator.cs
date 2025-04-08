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
            if (column.Role.HasFlag(TableColumnRole.PrimaryKey))
            {
                nullable = NOT_NULL;
                var colDataType = column.DataType;
                if (colDataType == ColumnDataType.Id)
                    colDataType = _meta.IdDataType;
                var defKey = colDataType switch
                {
                    ColumnDataType.Id => $"next value for {table.Schema}.SQ_{table.Name}",
                    ColumnDataType.Uniqueidentifier => "newid()",
                    ColumnDataType.Int or ColumnDataType.BigInt => $"next value for {table.Schema}.SQ_{table.Name}",
                    _ => throw new InvalidOperationException($"Defaults for {column.DataType} is not supported")
                };
                constraint = $"\n       constraint DF_{table.Name}_{column.Name} default({defKey})";
            }
            else if (column.HasDefault)
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

        var fields = table.Columns.Select(createField);

        var primaryKeys = table.PrimaryKeys.Select(c => $"[{c.Name}]");

        return $"""
        {createSequence()}

        if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'{table.Schema}' and TABLE_NAME=N'{table.Name}')
        create table {table.Schema}.[{table.Name}]
        (
            {String.Join(",\n    ", fields)},
            constraint PK_{table.Name} primary key ({String.Join(',', primaryKeys)})
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

        var fields = table.Columns.Select(createField);

        return $"""
        drop type if exists {table.Schema}.[{table.Name}.TableType];
        create type {table.Schema}.[{table.Name}.TableType] as table
        (
            {String.Join(",\n    ", fields)}
        );
        """;
    }

    internal String CreateForeignKeys(TableMetadata table)
    {
        String createReference(TableColumn column)
        {
            // TODO: Достать Id из таблицы
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
            return $"""
            if not exists(select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where TABLE_SCHEMA = N'{table.Schema}' and TABLE_NAME = N'{table.Name}' and CONSTRAINT_NAME = N'{constraintName}')
                alter table {table.SqlTableName} add 
                    constraint {constraintName} foreign key ([{column.Name}]) references {refs.RefSchema}.[{refs.RefTable}]([{refTablePkName}]);
            alter table {table.SqlTableName} nocheck constraint {constraintName};
            """;
        }
        var refs = table.Columns.Where(c => c.IsReference).Select(rc => createReference(rc));
        return String.Join('\n', refs);
    }
}

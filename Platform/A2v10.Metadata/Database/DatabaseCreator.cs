// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

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
                if (!column.Role.HasFlag(TableColumnRole.RowNo)) 
                {
                    var defKey = colDataType switch
                    {
                        ColumnDataType.Id => $"next value for {table.Schema}.SQ_{table.Name}",
                        ColumnDataType.Uniqueidentifier => "newsequentialid()",
                        ColumnDataType.Int or ColumnDataType.BigInt => $"next value for {table.Schema}.SQ_{table.Name}",
                        _ => throw new InvalidOperationException($"Defaults for {column.DataType} is not supported")
                    };
                    constraint = $"\n       constraint DF_{table.Name}_{column.Name} default({defKey})";
                }
            }
            else if (column.HasDefault)
            {
                nullable = NOT_NULL;
                constraint = $"\n       constraint DF_{table.Name}_{column.Name} default(0)";
            }
            return $"[{column.Name}] {column.SqlDataType(_meta.IdDataType)}{nullable}{constraint}";
        }

        String alterCreateField(TableColumn column)
        {
            return $"alter table {table.SqlTableName} add {createField(column)}";
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

        var alterFields = table.Columns
            .Where(c => String.IsNullOrEmpty(c.DbName) && !c.DbDataType.HasValue)
            .Select(alterCreateField);

        return $"""
        {createSequence()}

        if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'{table.Schema}' and TABLE_NAME=N'{table.Name}')
        create table {table.SqlTableName}
        (
            {String.Join(",\n    ", fields)},
            constraint PK_{table.Name} primary key ({String.Join(',', primaryKeys)})
        );
        {String.Join('\n', alterFields)}
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
        const String check = "check"; // TODO: ????
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
            alter table {table.SqlTableName} {check} constraint {constraintName};
            """;
        }
        var refs = table.Columns.Where(c => c.IsReference || c.DataType == ColumnDataType.Operation).Select(rc => createReference(rc));
        return String.Join('\n', refs);
    }

    internal String CreateOperations(IEnumerable<OperationMetadata> ops)
    {
        if (!ops.Any())
            return String.Empty;

        var opValues = ops.Select(op => $"""
        (N'{op.Id}', N'{op.Name ?? op.Id}', N'/operation/{op.Id.ToLowerInvariant()}')
        """);

        return $"""
        if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'op' and TABLE_NAME=N'Operations')
        create table op.[Operations] 
        (
            [Id] nvarchar(64) not null
                constraint PK_Operations primary key,
            [Void] bit not null
                constraint DF_Operations_Void default(0),
                    [Name] nvarchar(255),
            [Url] nvarchar(255)
        );
        
        begin
        declare @ops table(Id nvarchar(64), [Name] nvarchar(255), [Url] nvarchar(255));
        insert into @ops(Id, [Name], [Url]) values
        {String.Join(",\n", opValues)};
        
        merge op.Operations as t
        using @ops as s
        on t.Id = s.Id
        when matched then update set
            t.[Name] = s.[Name],
            t.[Url] = s.[Url]
        when not matched then insert
            (Id, [Name], [Url]) values
            (s.Id, s.[Name], s.[Url]);
        end
        """;
    }
}

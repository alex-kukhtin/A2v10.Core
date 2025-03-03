/*
Copyright © 2025 Oleksandr Kukhtin

Last updated : 03 mar 2025
module version : 8533
*/

------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2meta')
	exec sp_executesql N'create schema a2meta authorization dbo';
go
------------------------------------------------
grant execute on schema ::a2meta to public;
go

------------------------------------------------
create or alter procedure a2meta.[Table.Schema]
@Schema sysname,
@Table sysname
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @TenantId sysname = 'TenantId'

	select [Table!TTable!Object] = null, [Id!!Id] = 1, [Schema] = @Schema, [Table] = @Table,
		[TenantId] = @TenantId,
		[Columns!TColumn!Array] = null

	select [!TColumn!Array] = null, [Name!!Id] = COLUMN_NAME, DataType = DATA_TYPE, 
		[MaxLength] = CHARACTER_MAXIMUM_LENGTH,
		[Reference!TReference!Object] = null,
		[!TTable.Columns!ParentId] = 1
	from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = @Schema and TABLE_NAME = @Table and COLUMN_NAME <> @TenantId
	order by ORDINAL_POSITION;

	with T as (
		select [Name] = fk.[name], [index] = fkc.constraint_column_id,
			[schema] = schema_name(fk.[schema_id]),
			[table] = object_name(fk.parent_object_id),
			[Column] = c1.[name],
			RefSchema = schema_name(rt.[schema_id]),
			RefTable = object_name(fk.referenced_object_id),
			RefColumn = c2.[name]
		from  sys.foreign_keys fk inner join sys.foreign_key_columns fkc on fkc.constraint_object_id = fk.[object_id]
			inner join sys.tables rt on fk.referenced_object_id = rt.[object_id]
			inner join sys.columns c1 on fkc.parent_column_id = c1.column_id and fkc.parent_object_id = c1.[object_id]
			inner join sys.columns c2 on fkc.referenced_column_id = c2.column_id and fkc.referenced_object_id = c2.[object_id]
		where schema_name(fk.[schema_id]) = @Schema and object_name(fk.parent_object_id) = @Table and c1.[name] <> @TenantId
	)
	select [!TReference!Object] = null, RefSchema, RefTable, RefColumn,
		[!TColumn.Reference!ParentId] = [Column]
	from T;

	-- exetending properties
	-- https://www.mssqltips.com/sqlservertip/5384/working-with-sql-server-extended-properties/
end
go

------------------------------------------------

declare @Schema nvarchar(255) = N'doc';
declare @Table nvarchar(255) = N'Contracts';

exec a2meta.[Table.Schema] @Schema, @Table;



with T as (
	select fk.[name], [index] = fkc.constraint_column_id,
		[schema] = schema_name(fk.[schema_id]),
		[table] = object_name(fk.parent_object_id),
		[column] = c1.[name],
		refschema = schema_name(rt.[schema_id]),
		reftable = object_name(fk.referenced_object_id),
		refcolumn = c2.[name]
	from  sys.foreign_keys fk inner join sys.foreign_key_columns fkc on fkc.constraint_object_id = fk.[object_id]
		inner join sys.tables rt on fk.referenced_object_id = rt.[object_id]
		inner join sys.columns c1 on fkc.parent_column_id = c1.column_id and fkc.parent_object_id = c1.[object_id]
		inner join sys.columns c2 on fkc.referenced_column_id = c2.column_id and fkc.referenced_object_id = c2.[object_id]
	where schema_name(fk.[schema_id]) = 'doc' and object_name(fk.parent_object_id) = 'Contracts'
)
select [name], columns = string_agg([column], N','), 
refschema, reftable, refcolumns = string_agg(refcolumn, N',')
from T
group by [name], refschema, reftable;


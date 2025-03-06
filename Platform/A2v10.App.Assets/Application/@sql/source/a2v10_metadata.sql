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
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'TablesMetadata')
create table a2meta.TablesMetadata
(
	[Schema] sysname not null, 
	[Table] sysname not null,
	[Id] sysname null,
	[Name] sysname null,
	[Void] sysname null,
	[IsFolder] sysname null,
	[HiddenColumns] nvarchar(1024),
	constraint PK_TablesMetadata primary key([Schema], [Table])
);
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

	select [Table!TTable!Object] = null, [Id!!Id] = 1, [Schema] = TABLE_SCHEMA, [Table] = TABLE_NAME,
		[TenantId] = @TenantId,
		[Columns!TColumn!Array] = null,
		[Definition!TDefine!Object] = null
	from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA = @Schema collate SQL_Latin1_General_CP1_CI_AI 
		and TABLE_NAME = @Table collate SQL_Latin1_General_CP1_CI_AI;

	select [!TColumn!Array] = null, [Name!!Id] = COLUMN_NAME, DataType = DATA_TYPE, 
		[MaxLength] = CHARACTER_MAXIMUM_LENGTH,
		[Reference!TReference!Object] = null,
		[!TTable.Columns!ParentId] = 1
	from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = @Schema collate SQL_Latin1_General_CP1_CI_AI 
		and TABLE_NAME = @Table collate SQL_Latin1_General_CP1_CI_AI 
		and COLUMN_NAME <> @TenantId
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
		where schema_name(fk.[schema_id]) = @Schema collate SQL_Latin1_General_CP1_CI_AI
		and object_name(fk.parent_object_id) = @Table collate SQL_Latin1_General_CP1_CI_AI 
		and c1.[name] <> @TenantId
	)
	select [!TReference!Object] = null, RefSchema, RefTable, RefColumn,
		[!TColumn.Reference!ParentId] = [Column]
	from T;

	select [!TDefine!Object] = null, [Id], [Name], [Void], [HiddenColumns],
		[!TTable.Definition!ParentId] = 1
	from a2meta.TablesMetadata where [Schema] = @Schema collate SQL_Latin1_General_CP1_CI_AI 
		and [Table] = @Table collate SQL_Latin1_General_CP1_CI_AI;
	-- exetending properties
	-- https://www.mssqltips.com/sqlservertip/5384/working-with-sql-server-extended-properties/
end
go
------------------------------------------------
declare @Schema nvarchar(255) = N'CAT';
declare @Table nvarchar(255) = N'AGENTS';

exec a2meta.[Table.Schema] @Schema, @Table;

/*
insert into a2meta.TablesMetadata ([Schema], [Table], HiddenColumns)
values ('cat', 'Agents', N'Uid,Parent2');

insert into a2meta.TablesMetadata ([Schema], [Table], HiddenColumns)
values ('doc', 'Contracts', N'Uid');

insert into a2meta.TablesMetadata ([Schema], [Table], Name)
values ('cat', 'Currencies', N'Alpha3');
*/

select * from a2meta.TablesMetadata;

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



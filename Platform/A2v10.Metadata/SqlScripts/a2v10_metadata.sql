/*
Copyright © 2026 Oleksandr Kukhtin

Last updated : 25 jul 2026
module version : 8650
*/
------------------------------------------------
set nocount on;

if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2meta')
	exec sp_executesql N'create schema a2meta authorization dbo';
go
------------------------------------------------
alter authorization on schema::a2meta to dbo;
go
------------------------------------------------
grant execute on schema ::a2meta to public;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'SysParams')
create table a2meta.SysParams
(
	[name] sysname,
	[value] sysname,
	constraint PK_SysParams primary key ([name])
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Tables')
create table a2meta.Tables
(
	[schema] sysname,
	[table] sysname,
	constraint PK_Tables primary key ([schema], [table])
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Columns')
create table a2meta.Columns
(
	[schema] sysname,
	[table] sysname,
	[column] sysname,
	[datatype] sysname,      /* isnull(DOMAIN_NAME, DATA_TYPE) */
	[length] int,            /* CHARACTER_MAXIMUM_LENGTH: in characters, -1 means max */
	[precision] tinyint,     /* NUMERIC_PRECISION */
	[scale] tinyint,         /* NUMERIC_SCALE */
	[nullable] bit,          /* IS_NULLABLE */
	[ref_schema] nvarchar(128), /* foreign key target. Not sysname: sysname is nvarchar(128) NOT NULL */
	[ref_table] nvarchar(128),
	[default] nvarchar(128), /* default value for add column; takes no part in comparison.
	                            The constraint name is not stored - it is always DF_{table}_{column} */
	constraint PK_Columns primary key ([schema], [table], [column])
);
go
------------------------------------------------
create or alter procedure a2meta.[GetDbHash]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	select [Hash] = [value] from a2meta.SysParams where [name] = 'dbhash';
end
go
------------------------------------------------
create or alter procedure a2meta.[SetDbHash]
@Hash sysname
as
begin
	set nocount on;
	set transaction isolation level read committed;

	merge a2meta.SysParams t 
	using (select [name] = 'dbhash', [hash] = @Hash) s
	on t.[name] = s.[name]
	when matched then update set 
		t.[value] = s.[hash]
	when not matched then insert ([name], [value]) 
	values (s.[name], s.[hash]);

end
go
------------------------------------------------
create or alter procedure a2meta.[GetPlatformIdType]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	/* the base the platformid alias rests on. The database is the fact here, not a
	   declaration: it answers with the type the alias was actually created from.
	   No rows means the alias does not exist yet - that is an error, not a default. */
	select [DataType] = DATA_TYPE from INFORMATION_SCHEMA.DOMAINS where DOMAIN_NAME = N'platformid';
end
go
------------------------------------------------
create or alter procedure a2meta.[SyncSchema]
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;
end
go

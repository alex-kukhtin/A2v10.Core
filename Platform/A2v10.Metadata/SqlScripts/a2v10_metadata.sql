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
	[length] int,            /* CHARACTER_MAXIMUM_LENGTH: у символах, -1 = max */
	[precision] tinyint,     /* NUMERIC_PRECISION */
	[scale] tinyint,         /* NUMERIC_SCALE */
	[nullable] bit,          /* IS_NULLABLE */
	[ref_schema] nvarchar(128), /* ціль FK. Не sysname, бо sysname = nvarchar(128) NOT NULL */
	[ref_table] nvarchar(128),
	[default] nvarchar(128), /* значення дефолта для add column; у порівнянні не бере участі.
	                            Ім'я констрейнта не зберігається — воно завжди DF_{table}_{column} */
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
create or alter procedure a2meta.[SyncSchema]
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;
end
go

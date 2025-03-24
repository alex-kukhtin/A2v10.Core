/*
Copyright © 2025 Oleksandr Kukhtin

Last updated : 10 mar 2025
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
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2meta' and SEQUENCE_NAME=N'SQ_Catalog')
	create sequence a2meta.SQ_Catalog as bigint start with 100 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Catalog')
create table a2meta.[Catalog]
(
	[Id] bigint not null
		constraint DF_Catalog_Id default(next value for a2meta.SQ_Catalog)
		constraint PK_Catalog primary key,
	[Parent] bigint not null
		constraint FK_Catalog_Parent_Catalog references a2meta.[Catalog](Id),
	[ParentTable] bigint null
		constraint FK_Catalog_ParentTable_Catalog references a2meta.[Catalog](Id),
	IsFolder bit not null
		constraint DF_Catalog_IsFolder default(0),
	[Schema] nvarchar(32) null, 
	[Name] nvarchar(128) null,
	[Kind] nvarchar(32),
	ItemsName nvarchar(128),
	ItemName nvarchar(128),
	TypeName nvarchar(128)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'DefaultColumns')
create table a2meta.[DefaultColumns]
(
	[Id] bigint not null
		constraint PK_DefaultColumns primary key,
	[Schema] nvarchar(32),
	Kind nvarchar(32),
	[Name] nvarchar(128),
	[DataType] nvarchar(32),
	[MaxLength] int,
	Ref nvarchar(32)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2meta' and SEQUENCE_NAME=N'SQ_Columns')
	create sequence a2meta.SQ_Columns as bigint start with 100 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Columns')
create table a2meta.[Columns]
(
	[Id] bigint not null
		constraint DF_Columns_Id default(next value for a2meta.SQ_Columns)
		constraint PK_Columns primary key,
	[Table] bigint not null
		constraint FK_Columns_Parent_Catalog references a2meta.[Catalog](Id),
	[Name] nvarchar(128),
	[Label] nvarchar(255),
	[DataType] nvarchar(32),
	[MaxLength] int,
	Reference bigint
		constraint FK_Columns_Reference_Catalog references a2meta.[Catalog](Id)
);
go

------------------------------------------------
create or alter view a2meta.view_RealTables
as
	select Id = c.Id, c.Parent, c.Kind, c.[Schema], [Name] = c.[Name], c.ItemsName, c.ItemName, c.TypeName
	from a2meta.[Catalog] c inner join INFORMATION_SCHEMA.TABLES ic on 
		ic.TABLE_SCHEMA = c.[Schema]
		and ic.TABLE_NAME = c.[Name] collate SQL_Latin1_General_CP1_CI_AI;
go
------------------------------------------------
create or alter procedure a2meta.[Table.Schema]
@Schema sysname,
@Table sysname
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @tableId bigint;

	select @tableId = Id from a2meta.view_RealTables r 
	where r.[Schema] = @Schema collate SQL_Latin1_General_CP1_CI_AI
		and r.[Name] = @Table collate SQL_Latin1_General_CP1_CI_AI
		and r.Kind = N'table';

	declare @innerTables table(Id bigint, Kind nvarchar(32));
	with TT as (
		select Id, Parent, Kind from a2meta.[Catalog] where Id = @tableId and Kind = N'table'
		union all 
		select c.Id, c.Parent, c.Kind 
		from a2meta.[Catalog] c
			inner join TT on c.Parent = tt.Id and c.Kind in (N'table', N'details')
	)
	insert into @innerTables (Id, Kind)
	select Id, Kind from TT;

	select [Table!TTable!Object] = null, [!!Id] = c.Id, c.[Schema], c.[Name],
		c.ItemsName, c.ItemName, c.TypeName,
		[Columns!TColumn!Array] = null,
		[Details!TTable!Array] = null
	from a2meta.view_RealTables c 
	where c.Id = @tableId and c.Kind = N'table';

	select [!TTable!Array] = null, [Id!!Id] = c.Id, [Schema] = c.[Schema], [Name] = c.[Name],
		c.ItemsName, c.ItemName, c.TypeName,
		[Columns!TColumn!Array] = null,
		[!TTable.Details!ParentId] = c.Parent
	from a2meta.view_RealTables c 
	where c.Parent = @tableId and c.Kind = N'details';

	select [!TColumn!Array] = null, [Id!!Id] = c.Id, c.[Name], DataType = c.DataType, 
		c.[MaxLength],
		[Reference.RefSchema!TReference!] = r.[Schema],
		[Reference.RefTable!TReference!] = r.[Name],
		[!TTable.Columns!ParentId] = c.[Table]
	from a2meta.Columns c
		inner join @innerTables it on c.[Table] = it.Id
		left join a2meta.[Catalog] r on c.Reference = r.Id
	order by c.Id;
end
go

------------------------------------------------
begin
	declare @cat table(Id bigint, IsFolder bit, Parent bigint, [Schema] nvarchar(32), [Name] nvarchar(255), Kind nvarchar(32));
	insert into @cat(Id, IsFolder, [Schema], Kind, [Name]) values
	(0,  0, N'root', N'root',   N'Root'),
	(10, 0, N'app',  N'app',    N'Application'),
	(11, 1, N'cat',  N'folder', N'Catalogs'),
	(12, 1, N'doc',  N'folder', N'Documents'),
	(13, 1, N'jrn',  N'folder', N'Journals'),
	(14, 1, N'rep',  N'folder', N'Reports'),
	(70, 1, N'ui',   N'folder', N'User Interfaces');

	merge a2meta.[Catalog] as t
	using @cat as s
	on t.Id = s.Id
	when matched then update set
		t.[Name] = s.[Name],
		t.Kind = s.Kind,
		t.IsFolder = s.IsFolder
	when not matched then insert
		(Id, Parent, IsFolder, [Schema], [Name], Kind) values
		(s.Id, 0, s.IsFolder, s.[Schema], s.[Name], Kind);

	declare @defCols table(Id bigint, [Schema] nvarchar(32), Kind nvarchar(32), [Name] nvarchar(255), 	[DataType] nvarchar(32),
		[MaxLength] int, Ref nvarchar(32));

	insert into @defCols(Id, [Schema], Kind, [Name], DataType, [MaxLength], Ref) values
	(10, N'cat', N'table', N'Id',   N'id', null, null),
	(11, N'cat', N'table', N'Void', N'bit', null, null),
	(12, N'cat', N'table', N'IsSystem', N'bit', null, null),
	(13, N'cat', N'table', N'IsFolder', N'bit', null, null),
	(14, N'cat', N'table', N'Parent', N'reference', null, N'self'),
	(15, N'cat', N'table', N'Name', N'string',    255, null),
	(16, N'cat', N'table', N'Memo', N'string',    255, null),
	(17, N'cat', N'table', N'Owner',N'reference', null, N'user'),
	(20, N'doc', N'table', N'Id',   N'id',       null, null),
	(21, N'doc', N'table', N'Void', N'bit',      null, null),
	(22, N'doc', N'table', N'Done', N'bit',      null, null),
	(23, N'doc', N'table', N'Date', N'date',     null, null),
	(24, N'doc', N'table', N'Sum',  N'currency', null, null),
	(25, N'doc', N'table', N'Memo', N'string', 255, null),
	(26, N'doc', N'table', N'Owner',N'reference', null, N'user'),
	-- cat.Details
	(30, N'cat', N'details', N'Id',     N'id', null, null),
	(31, N'cat', N'details', N'Parent', N'reference', null, N'parent'),
	(32, N'cat', N'details', N'RowNo',  N'int', null, null),
	-- doc.Details
	(40, N'doc', N'details', N'Id',     N'id', null, null),
	(41, N'doc', N'details', N'Parent', N'reference', null, N'parent'),
	(42, N'doc', N'details', N'RowNo',  N'int', null, null),
	-- jrn.Journal
	(50, N'jrn', N'table', N'Id',     N'id', null, null),
	(51, N'jrn', N'table', N'Date',   N'datetime', null, null),
	(52, N'jrn', N'table', N'InOut',  N'int', null, null),
	(53, N'jrn', N'table', N'Owner',  N'reference', null, N'user');

	merge a2meta.DefaultColumns as t
	using @defCols as s
	on t.Id = s.Id
	when matched then update set
		t.[Schema] = s.[Schema],
		t.[Kind] = s.Kind,
		t.[Name] = s.[Name],
		t.DataType = s.DataType,
		t.[MaxLength] = s.[MaxLength],
		t.Ref = s.Ref
	when not matched then insert
		(Id, [Schema], Kind, [Name], DataType, [MaxLength], Ref) values
		(Id, [Schema], Kind, [Name], DataType, [MaxLength], s.Ref);
end
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
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'TableDetails')
create table a2meta.TableDetails
(
	[ParentSchema] sysname not null, 
	[ParentTable] sysname not null,
	[DetailsSchema] sysname not null,
	[DetailsTable] sysname not null,
	SameId bit not null
		constraint DF_TableDetails_SameId default(0),
	constraint PK_TableDetails primary key([ParentSchema], [ParentTable], [DetailsSchema], [DetailsTable])
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2meta' and SEQUENCE_NAME=N'SQ_TableForms')
	create sequence a2meta.SQ_TableForms as bigint start with 100 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'TableForms')
create table a2meta.TableForms
(
	[Id] bigint not null
		constraint DF_Forms_Id default(next value for a2meta.SQ_TableForms)
		constraint PK_Forms primary key,
	[Key] sysname not null,
	[Schema] sysname not null, 
	[Table] sysname not null,
	Width int,
	Title nvarchar(255)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2meta' and SEQUENCE_NAME=N'SQ_FormColumns')
	create sequence a2meta.SQ_FormColumns as bigint start with 100 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'FormColumns')
create table a2meta.FormColumns
(
	[Id] bigint not null
		constraint DF_FormColumns_Id default(next value for a2meta.SQ_FormColumns)
		constraint PK_FormColumns primary key,
	[Form] bigint not null
		constraint FK_FormColumns_Form_TableForms references a2meta.TableForms(Id),
	[Order] int not null,
	[Path] nvarchar(255), 
	Header nvarchar(255),
	NoSort bit,
	[Filter] bit,
	Fit bit,
	Width int,
	Clamp int
);
go
------------------------------------------------
drop procedure if exists a2meta.[TableDetails.Merge];
drop type if exists a2meta.[TableDetails.TableType];
go
------------------------------------------------
create type a2meta.[TableDetails.TableType] as table (
	[Schema] sysname not null,
	[Table] sysname not null,
	SameId bit not null
);
go
------------------------------------------------
create or alter procedure a2meta.[TableDetails.Merge]
@ParentSchema sysname,
@ParentTable sysname,
@Details a2meta.[TableDetails.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;
    merge a2meta.[TableDetails] as t
    using @Details as s
    on t.ParentSchema = @ParentSchema and t.ParentTable = @ParentTable and  t.[DetailsSchema] = s.[Schema] and t.[DetailsTable] = s.[Table]
    when not matched then insert
        (ParentSchema, ParentTable, [DetailsSchema], [DetailsTable], SameId) values
        (@ParentSchema, @ParentTable, s.[Schema], s.[Table], s.SameId);
end
go
------------------------------------------------
create or alter procedure a2meta.[App.Metadata]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	select [Application!TApp!Object] = null, IdDataType = N'bigint', 
		[Id] = cast(null as nvarchar(32)), 
		[Name] = cast(null as nvarchar(32)), 
		Void = cast(null as nvarchar(32)), 
		IsFolder = cast(null as nvarchar(32)), 
		IsSystem = cast(null as nvarchar(32));
end
go
------------------------------------------------
create or alter function a2meta.fn_Schema2Text(@Schema nvarchar(32))
returns nvarchar(255)
as
begin
	return case @Schema
		when N'cat' then N'Catalogs'
		when N'doc' then N'Documents'
		when N'jrn' then N'Journals'
		when N'rep' then N'Reports'
		else N'Undefined'
	end;
end
go
------------------------------------------------
create or alter function a2meta.fn_TableFullName(@Schema nvarchar(32), @Name nvarchar(128))
returns nvarchar(255)
as
begin
	return a2meta.fn_Schema2Text(@Schema) + N'.' + @Name;
end
go
------------------------------------------------
create or alter procedure a2meta.[Config.Load]
@UserId bigint
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Application!TApp!Object] = null, [!!Id] = 1, IdDataType = N'bigint', 
		[Id] = cast(null as nvarchar(32)), 
		[Name] = cast(null as nvarchar(32)), 
		Void = cast(null as nvarchar(32)), 
		IsFolder = cast(null as nvarchar(32)), 
		IsSystem = cast(null as nvarchar(32)), 
		[Tables!TTable!Array] = null;

	select [!TTable!Array] = null, [Id!!Id] = Id, c.[Schema], c.[Name], c.[Kind],
		[Columns!TColumn!Array] = null,
		[!TApp.Tables!ParentId] = 1
	from a2meta.[Catalog] c
		left join INFORMATION_SCHEMA.TABLES t on t.TABLE_SCHEMA =  c.[Schema] and t.TABLE_NAME = c.[Name]
	where c.[Kind] in (N'table', N'details');

	select [!TColumn!Array] = null, [Id!!Id] = c.Id, c.[Name], c.[DataType], c.[MaxLength],
		[Reference.RefSchema!TRef!] = r.[Schema], [Reference.RefTable!TRef!] = r.[Name],
		DbName = ic.COLUMN_NAME, DbDataType =  ic.DATA_TYPE,
		[!TTable.Columns!ParentId] = c.[Table]
	from a2meta.Columns c
		inner join a2meta.[Catalog] t on c.[Table] = t.Id 
		left join a2meta.[Catalog] r on c.Reference = r.Id
		left join INFORMATION_SCHEMA.COLUMNS ic on ic.TABLE_SCHEMA = t.[Schema] and ic.TABLE_NAME = t.[Name] and ic.COLUMN_NAME = c.[Name];
end
go

------------------------------------------------
create or alter procedure a2meta.[Table.Form]
@Schema sysname,
@Table sysname,
@Key sysname
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @formId bigint;
	select @formId = Id from a2meta.TableForms 
	where [Schema] = @Schema collate SQL_Latin1_General_CP1_CI_AI 
		and [Table] = @Table collate SQL_Latin1_General_CP1_CI_AI 
		and [Key] = @Key collate SQL_Latin1_General_CP1_CI_AI;

	select [Form!TForm!Object] = null, [Id!!Id] = Id, [Key],
		Width, Title,
		[Columns!TFColumn!Array] = null, [Controls!TFControl!Array] = null
	from a2meta.TableForms where Id = @formId;

	select [!TFColumn!Array] = null, [Id!!Id] = Id, [Path], Header, 
		Width, Clamp, NoSort, [Filter], Fit,
		[!TForm.Columns!ParentId] = [Form]
	from a2meta.FormColumns where [Form] = @formId
	order by [Order]
end
go
------------------------------------------------
/*
declare @Schema nvarchar(255) = N'cat';
declare @Table nvarchar(255) = N'Companies';

exec a2meta.[Table.Schema] @Schema, @Table;
*/

/*
insert into a2meta.TablesMetadata ([Schema], [Table], HiddenColumns)
values ('cat', 'Agents', N'Uid,Parent2');

insert into a2meta.TableForms ([Schema], [Table], [Key], Width)
values ('cat', 'Agents', N'Index', 800);

insert into a2meta.TablesMetadata ([Schema], [Table], HiddenColumns)
values ('doc', 'Contracts', N'Uid');

insert into a2meta.TablesMetadata ([Schema], [Table], Name)
values ('cat', 'Currencies', N'Alpha3');
*/

--select * from a2meta.TablesMetadata;

/*
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
*/

/*
drop table a2meta.Columns
drop table a2meta.[Catalog];
*/

--exec a2meta.[Config.Load] 99

declare @Schema nvarchar(255) = N'doc';
declare @Table nvarchar(255) = N'documents';

exec a2meta.[Table.Schema] @Schema, @Table;

select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where CONSTRAINT_NAME = N'FK_Columns_Parent_Catalog'

/*
	select [!TColumn!Array] = null, [Name!!Id] = COLUMN_NAME, DataType = DATA_TYPE, 
		[MaxLength] = CHARACTER_MAXIMUM_LENGTH,
		[Reference!TReference!Object] = null,
		[!TTable.Columns!ParentId] = @tableId
	from INFORMATION_SCHEMA.COLUMNS where 
		TABLE_SCHEMA = @Schema collate SQL_Latin1_General_CP1_CI_AI 
		and TABLE_NAME = @Table collate SQL_Latin1_General_CP1_CI_AI 
	order by ORDINAL_POSITION;
*/

/*
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
	)
	select [!TReference!Object] = null, RefSchema, RefTable, RefColumn,
		[!TColumn.Reference!ParentId] = [Column]
	from T;
*/

	
	-- exetending properties
	-- https://www.mssqltips.com/sqlservertip/5384/working-with-sql-server-extended-properties/

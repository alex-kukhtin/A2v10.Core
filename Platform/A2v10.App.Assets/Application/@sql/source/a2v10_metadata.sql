/*
Copyright � 2025 Oleksandr Kukhtin

Last updated : 02 apr 2025
module version : 8540
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
	TypeName nvarchar(128),
	EditWith nvarchar(16)
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
		constraint FK_Columns_Table_Catalog references a2meta.[Catalog](Id),
	[Name] nvarchar(128),
	[Label] nvarchar(255),
	[DataType] nvarchar(32),
	[MaxLength] int,
	Reference bigint
		constraint FK_Columns_Reference_Catalog references a2meta.[Catalog](Id),
	[Order] int,
	IsSystem bit
		constraint DF_Columns_IsSystem default(0)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2meta' and SEQUENCE_NAME=N'SQ_Items')
	create sequence a2meta.SQ_Items as bigint start with 100 increment by 1;
go
------------------------------------------------
-- TODO: DELETE ME
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Items')
create table a2meta.Items
(
	[Id] bigint not null
		constraint DF_Items_Id default(next value for a2meta.SQ_Items)
		constraint PK_Items primary key,
	[Table] bigint not null
		constraint FK_Items_Table_Catalog references a2meta.[Catalog](Id),
	[Name] nvarchar(128),
	[Code] nvarchar(64),
	[Label] nvarchar(255),
	[Order] int
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Forms')
create table a2meta.[Forms]
(
	[Table] bigint not null
		constraint FK_Forms_Table_Catalog references a2meta.[Catalog](Id),
	[Key] nvarchar(64) not null,
	[Json] nvarchar(max),
		constraint PK_Forms primary key ([Table], [Key]) 
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2meta' and SEQUENCE_NAME=N'SQ_Apply')
	create sequence a2meta.SQ_Apply as bigint start with 100 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Apply')
create table a2meta.[Apply]
(
	[Id] bigint not null
		constraint DF_Apply_Id default(next value for a2meta.SQ_Apply)
		constraint PK_Apply primary key,
	[Table] bigint not null
		constraint FK_Apply_Table_Catalog references a2meta.[Catalog](Id),
	[Journal] bigint not null
		constraint FK_Apply_Journal_Catalog references a2meta.[Catalog](Id),
	[Order] int not null,
	Details bigint
		constraint FK_Apply_Details_Catalog references a2meta.[Catalog](Id),
	[InOut] smallint not null,
	Storno bit not null
		constraint DF_Apply_Storno default(0)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2meta' and SEQUENCE_NAME=N'SQ_ApplyMapping')
	create sequence a2meta.SQ_ApplyMapping as bigint start with 100 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'ApplyMapping')
create table a2meta.[ApplyMapping]
(
	[Id] bigint not null
		constraint DF_ApplyMapping_Id default(next value for a2meta.SQ_ApplyMapping)
		constraint PK_ApplyMapping primary key,
	[Apply] bigint not null
		constraint FK_ApplyMapping_Apply_Apply references a2meta.Apply(Id),
	[Target] bigint not null
		constraint FK_ApplyMapping_Target_Columns references a2meta.Columns(Id),
	[Source] bigint not null
		constraint FK_ApplyMapping_Source_Columns references a2meta.Columns(Id)
);
go
------------------------------------------------
create or alter view a2meta.view_RealTables
as
	select Id = c.Id, c.Parent, c.Kind, c.[Schema], [Name] = c.[Name], c.ItemsName, c.ItemName, c.TypeName,
		c.EditWith, c.ParentTable
	from a2meta.[Catalog] c left join INFORMATION_SCHEMA.TABLES ic on 
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
		and r.Kind in (N'table', N'operation');

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
		c.ItemsName, c.ItemName, c.TypeName, c.EditWith,
		[ParentTable.RefSchema!TReference!] = pt.[Schema], [ParentTable.RefTable!TReference] = pt.[Name],
		[Columns!TColumn!Array] = null,
		[Details!TTable!Array] = null,
		[Apply!TApply!Array] = null
	from a2meta.view_RealTables c 
		left join a2meta.[Catalog] pt on c.ParentTable = pt.Id
	where c.Id = @tableId and c.Kind in (N'table', N'operation');

	select [!TTable!Array] = null, [Id!!Id] = c.Id, [Schema] = c.[Schema], [Name] = c.[Name],
		c.ItemsName, c.ItemName, c.TypeName,
		[Columns!TColumn!Array] = null,
		[!TTable.Details!ParentId] = c.Parent
	from a2meta.view_RealTables c 
	where c.Parent = @tableId and c.Kind = N'details';

	select [!TColumn!Array] = null, [Id!!Id] = c.Id, c.[Name], DataType = c.DataType, 
		c.[MaxLength],
		[Reference.RefSchema!TReference!] = case c.DataType 
		when N'operation' then N'op' 
		else r.[Schema] 
		end,
		[Reference.RefTable!TReference!] = case c.DataType 
		when N'operation' then N'Operations'
		else r.[Name]
		end,
		[!TTable.Columns!ParentId] = c.[Table]
	from a2meta.Columns c
		inner join @innerTables it on c.[Table] = it.Id
		left join a2meta.[Catalog] r on c.Reference = r.Id
	order by c.Id;

	select [!TApply!Array] = null, [Id!!Id] = a.Id, a.InOut, a.Storno,
		[Journal.RefSchema!TReference!] = j.[Schema], [Journal.RefTable!TReference!Name] = j.[Name],
		[Details.RefSchema!TReference!] = d.[Schema], [Details.RefTable!TReference!Name] = d.[Name],
		[Mapping!TMapping!Array] = null,
		[!TTable.Apply!ParentId] = a.[Table]
	from a2meta.Apply a 
		inner join a2meta.[Catalog] j on a.Journal = j.Id -- always
		left join a2meta.[Catalog] d on a.Details = d.Id -- possible
	where a.[Table] = @tableId;

	select [!TMapping!Array] = null, [Id!!Id] = m.Id,
		[Target] = t.[Name], 
		-- source may be in document or details
		[Source] = s.[Name], Kind = st.Kind,
		[!TApply.Mapping!ParentId] = a.Id
	from a2meta.ApplyMapping m 
		inner join a2meta.[Apply] a on m.Apply = a.Id
		inner join a2meta.Columns t on m.[Target] = t.Id
		inner join a2meta.Columns s on m.Source= s.Id
		inner join a2meta.[Catalog] st on s.[Table] = st.Id
	where a.[Table] = @tableId;
end
go
------------------------------------------------
create or alter procedure a2meta.[Table.Form]
@Schema nvarchar(32) = null,
@Table nvarchar(128) = null,
@Id bigint = null,
@Key nvarchar(64),
@WithColumns bit = 0
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	if @Id is null and @Schema is not null and @Table is not null
		select @Id = Id from a2meta.[Catalog] where 
			[Schema] = @Schema  collate SQL_Latin1_General_CP1_CI_AI
			and [Name] = @Table  collate SQL_Latin1_General_CP1_CI_AI;

	select [Table!TTable!Object] = null, [Id!!Id] = Id, [Name], [Schema], EditWith, ParentTable
	from a2meta.[Catalog] where Id = @Id;

	select [Form!TForm!Object] = null, [Id!!Id] = @Id,  [Key],
		[Json!!Json] = f.[Json]
	from a2meta.Forms f where [Table] = @Id and [Key] = @Key;

	select [Columns!TColumn!Array] = null, [Id!!Id] = Id, c.[Name], c.[Label], c.DataType, c.Reference
	from a2meta.Columns c where [Table] = @Id;
end
go
------------------------------------------------
create or alter procedure a2meta.[Table.Form.Update]
@Id bigint = null,
@Key nvarchar(64),
@Json nvarchar(max),
@WithColumns bit = 0
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2meta.Forms set [Json] = @Json where [Table] = @Id and [Key] = @Key;
	if @@rowcount = 0
		insert into a2meta.Forms ([Table], [Key], [Json]) values (@Id, @Key, @Json);

	exec a2meta.[Table.Form] @Id = @Id, @Key = @Key;
end
go
------------------------------------------------
begin
	declare @cat table(Id bigint, IsFolder bit, Parent bigint, [Schema] nvarchar(32), [Name] nvarchar(255), Kind nvarchar(32));
	insert into @cat(Id, IsFolder, [Schema], Kind, [Name]) values
	(0,  0, N'root', N'root',   N'Root'),
	(10, 0, N'app',  N'app',    N'Application'),
	(11, 1, N'enm',  N'folder', N'Enums'),
	(12, 1, N'cat',  N'folder', N'Catalogs'),
	(13, 1, N'doc',  N'folder', N'Documents'),
	(14, 1, N'op',   N'folder', N'Operations'),
	(15, 1, N'jrn',  N'folder', N'Journals'),
	(16, 1, N'rep',  N'folder', N'Reports'),
	(70, 1, N'ui',   N'folder', N'User Interfaces');

	merge a2meta.[Catalog] as t
	using @cat as s
	on t.Id = s.Id
	when matched then update set
		t.[Name] = s.[Name],
		t.Kind = s.Kind,
		t.IsFolder = s.IsFolder,
		t.[Schema] = s.[Schema]
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
		when N'cat' then N'Catalog'
		when N'doc' then N'Document'
		when N'jrn' then N'Journal'
		when N'rep' then N'Report'
		when N'op'  then N'Operation'
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

select * from a2meta.Columns where Id = 354;

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

/*
Copyright © 2025 Oleksandr Kukhtin

Last updated : 12 may 2025
module version : 8547
*/
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2meta')
	exec sp_executesql N'create schema a2meta authorization dbo';
go
------------------------------------------------
grant execute on schema ::a2meta to public;
go
------------------------------------------------
create or alter view a2meta.view_TableTypeColumns 
as
	select [schema] = schema_name(t.[schema_id]),
		column_name = c.[name],
		column_id = c.column_id,
		[type_name] = t.[name]
	from sys.columns c inner join sys.table_types t on c.[object_id] = t.type_table_object_id
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Catalog')
create table a2meta.[Catalog]
(
	[Id] uniqueidentifier not null
		constraint DF_Catalog_Id default(newid())
		constraint PK_Catalog primary key,
	[Parent] uniqueidentifier not null
		constraint FK_Catalog_Parent_Catalog references a2meta.[Catalog](Id),
	[ParentTable] uniqueidentifier null
		constraint FK_Catalog_ParentTable_Catalog references a2meta.[Catalog](Id),
	IsFolder bit not null
		constraint DF_Catalog_IsFolder default(0),
	[Order] int null,
	[Schema] nvarchar(32) null, 
	[Name] nvarchar(128) null,
	[Kind] nvarchar(32),
	ItemsName nvarchar(128),
	ItemName nvarchar(128),
	TypeName nvarchar(128),
	EditWith nvarchar(16),
	Source nvarchar(255),
	ItemsLabel nvarchar(255),
	ItemLabel nvarchar(128),
	UseFolders bit
		constraint DF_Catalog_UseFolders default(0),
	FolderMode nvarchar(16),
	[Type] nvarchar(32) -- for reports, other
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Application')
create table a2meta.[Application]
(
	[Id] uniqueidentifier not null
		constraint PK_Application primary key
		constraint FK_Application_Id_Catalog references a2meta.[Catalog](Id),
	[Name] nvarchar(255),
	[Title] nvarchar(255),
	IdDataType nvarchar(32),
	Memo nvarchar(255),
	[Version] int not null
		constraint DF_Application_Version default(0)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'DefaultColumns')
create table a2meta.[DefaultColumns]
(
	[Id] uniqueidentifier not null
		constraint DF_DefaultColumns_Id default(newid())
		constraint PK_DefaultColumns primary key,
	[Schema] nvarchar(32),
	Kind nvarchar(32),
	[Name] nvarchar(128),
	[DataType] nvarchar(32),
	[MaxLength] int,
	Ref nvarchar(32),
	[Role] int not null
		constraint DF_DefaultColumns_Role default(0),
	[Order] int not null,
	[Required] bit not null
		constraint DF_DefaultColumns_Required default(0)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Columns')
create table a2meta.[Columns]
(
	[Id] uniqueidentifier not null
		constraint DF_Columns_Id default(newid())
		constraint PK_Columns primary key,
	[Table] uniqueidentifier not null
		constraint FK_Columns_Table_Catalog references a2meta.[Catalog](Id),
	[Name] nvarchar(128),
	[Label] nvarchar(255),
	[DataType] nvarchar(32),
	[MaxLength] int,
	Reference uniqueidentifier
		constraint FK_Columns_Reference_Catalog references a2meta.[Catalog](Id),
	[Order] int,
	[Role] int not null
		constraint DF_Columns_Role default(0),
	Source nvarchar(255) null,
	Computed nvarchar(255) null,
	[Required] bit
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'ReportItems')
create table a2meta.[ReportItems]
(
	[Id] uniqueidentifier not null
		constraint DF_ReportItems_Id default(newid())
		constraint PK_ReportItems primary key,
	[Report] uniqueidentifier not null
		constraint FK_ReportItems_Report_Catalog references a2meta.[Catalog](Id),
	[Column] uniqueidentifier not null
		constraint FK_ReportItems_Column_Columns references a2meta.[Columns](Id),
	[Kind] nchar(1) not null, -- (G)roup, (F)ilter, (D)ata, (A)ttribute
	[Order] int not null,
	[Label] nvarchar(255),
	Func nvarchar(32), -- for Data - Year, Quart, Month
	[Checked] bit -- for Grouping
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'EnumItems')
create table a2meta.EnumItems
(
	[Id] uniqueidentifier not null
		constraint DF_EnumItems_Id default(newid())
		constraint PK_EnumItems primary key,
	[Enum] uniqueidentifier not null
		constraint FK_EnumItems_Report_Catalog references a2meta.[Catalog](Id),
	[Name] nvarchar(16) not null,
	[Label] nvarchar(255),
	[Order] int not null,
	[Inactive] bit
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'DefaultSections')
create table a2meta.[DefaultSections]
(
	[Id] uniqueidentifier not null
		constraint DF_DefaultSections_Id default(newid())
		constraint PK_DefaultSections primary key,
	[Schema] nvarchar(32) not null,
	[Name] nvarchar(255),
	[Order] int not null
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'MenuItems')
create table a2meta.[MenuItems]
(
	[Id] uniqueidentifier not null
		constraint DF_MenuItems_Id default(newid())
		constraint PK_MenuItems primary key,
	[Interface] uniqueidentifier not null
		constraint FK_MenuItems_Interface_Catalog references a2meta.[Catalog](Id)
		on delete cascade,
	[Parent] uniqueidentifier
		constraint FK_MenuItems_Parent_MenuItems references a2meta.[MenuItems](Id),
	[Name] nvarchar(255),
	[Url] nvarchar(255),
	[CreateName] nvarchar(255),
	[CreateUrl] nvarchar(255),
	[Order] int,
	Source nvarchar(255) null
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'DetailsKinds')
create table a2meta.[DetailsKinds]
(
	[Id] uniqueidentifier not null
		constraint DF_DetailsKinds_Id default(newid())
		constraint PK_DetailsKinds primary key,
	[Details] uniqueidentifier not null
		constraint FK_DetailsKinds_Table_Catalog references a2meta.[Catalog](Id),
	[Order] int not null,
	[Name] nvarchar(32),
	[Label] nvarchar(255)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Forms')
create table a2meta.[Forms]
(
	[Table] uniqueidentifier not null
		constraint FK_Forms_Table_Catalog references a2meta.[Catalog](Id),
	[Key] nvarchar(64) not null,
	[Json] nvarchar(max),
		constraint PK_Forms primary key ([Table], [Key]) 
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Apply')
create table a2meta.[Apply]
(
	[Id] uniqueidentifier not null
		constraint DF_Apply_Id default(newid())
		constraint PK_Apply primary key,
	[Table] uniqueidentifier not null
		constraint FK_Apply_Table_Catalog references a2meta.[Catalog](Id),
	[Journal] uniqueidentifier not null
		constraint FK_Apply_Journal_Catalog references a2meta.[Catalog](Id),
	[Order] int not null,
	Details uniqueidentifier
		constraint FK_Apply_Details_Catalog references a2meta.[Catalog](Id),
	[InOut] smallint not null,
	Storno bit not null
		constraint DF_Apply_Storno default(0),
	Kind uniqueidentifier
		constraint FK_Apply_Kind_DetailsKinds references a2meta.DetailsKinds(Id),
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'ApplyMapping')
create table a2meta.[ApplyMapping]
(
	[Id] uniqueidentifier not null
		constraint DF_ApplyMapping_Id default(newid())
		constraint PK_ApplyMapping primary key,
	[Apply] uniqueidentifier not null
		constraint FK_ApplyMapping_Apply_Apply references a2meta.Apply(Id) on delete cascade,
	[Target] uniqueidentifier not null
		constraint FK_ApplyMapping_Target_Columns references a2meta.Columns(Id),
	[Source] uniqueidentifier not null
		constraint FK_ApplyMapping_Source_Columns references a2meta.Columns(Id)
);
go
------------------------------------------------
create or alter view a2meta.view_RealTables
as
	select Id = c.Id, c.Parent, c.Kind, c.[Schema], [Name] = c.[Name], c.ItemsName, c.ItemName, c.TypeName,
		c.EditWith, c.ParentTable, c.IsFolder, c.ItemLabel, c.ItemsLabel, c.UseFolders, c.[Type]
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

	declare @tableId uniqueidentifier;

	select @tableId = Id from a2meta.view_RealTables r 
	where r.[Schema] = @Schema collate SQL_Latin1_General_CP1_CI_AI
		and r.[Name] = @Table collate SQL_Latin1_General_CP1_CI_AI
		and r.Kind in (N'table', N'operation');

	declare @innerTables table(Id uniqueidentifier, Kind nvarchar(32), [Schema] sysname, [Name] sysname);
	with TT as (
		select Id, Parent, Kind, [Schema], [Name] from a2meta.[Catalog] where Id = @tableId and Kind = N'table'
		union all 
		select c.Id, c.Parent, c.Kind, c.[Schema], c.[Name]
		from a2meta.[Catalog] c
			inner join TT on c.Parent = tt.Id and c.Kind in (N'table', N'details')
	)
	insert into @innerTables (Id, Kind, [Schema], [Name])
	select Id, Kind, [Schema], [Name] from TT;

	select [Table!TTable!Object] = null, [!!Id] = c.Id, c.[Schema], c.[Name],
		c.ItemsName, c.ItemName, c.TypeName, c.EditWith, c.ItemLabel, c.ItemsLabel, c.UseFolders,
		[ParentTable.RefSchema!TReference!] = pt.[Schema], [ParentTable.RefTable!TReference] = pt.[Name],
		[Columns!TColumn!Array] = null,
		[Details!TTable!Array] = null,
		[Apply!TApply!Array] = null,
		[Kinds!TKind!Array] = null
	from a2meta.view_RealTables c 
		left join a2meta.[Catalog] pt on c.ParentTable = pt.Id
	where c.Id = @tableId and c.Kind in (N'table', N'operation');

	select [!TTable!Array] = null, [Id!!Id] = c.Id, [Schema] = c.[Schema], [Name] = c.[Name],
		c.ItemsName, c.ItemName, c.TypeName, c.ItemLabel, c.ItemsLabel, c.[Type],
		[Columns!TColumn!Array] = null,
		[Kinds!TKind!Array] = null,
		[!TTable.Details!ParentId] = c.Parent
	from a2meta.view_RealTables c 
	where c.Parent = @tableId and c.Kind = N'details';

	select [!TColumn!Array] = null, [Id!!Id] = c.Id, c.[Name], c.[Label], c.DataType, 
		c.[MaxLength], c.[Role], c.Computed, c.[Required], c.[Order], DbOrder = tvc.column_id,
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
		inner join a2meta.view_TableTypeColumns tvc on c.[Name] = tvc.column_name 
			and tvc.[schema] = it.[Schema] collate SQL_Latin1_General_CP1_CI_AI
			and tvc.[type_name] = it.[Name] + N'.TableType' collate SQL_Latin1_General_CP1_CI_AI
		left join a2meta.[Catalog] r on c.Reference = r.Id
	order by it.[Name], tvc.column_id; -- same as [Config.Load]

	select [!TApply!Array] = null, [Id!!Id] = a.Id, a.InOut, a.Storno, DetailsKind = dk.[Name],
		[Journal.RefSchema!TReference!] = j.[Schema], [Journal.RefTable!TReference!Name] = j.[Name],
		[Details.RefSchema!TReference!] = d.[Schema], [Details.RefTable!TReference!Name] = d.[Name],
		[Mapping!TMapping!Array] = null,
		[!TTable.Apply!ParentId] = a.[Table]
	from a2meta.Apply a 
		inner join a2meta.[Catalog] j on a.Journal = j.Id -- always
		left join a2meta.[Catalog] d on a.Details = d.Id -- possible
		left join a2meta.DetailsKinds dk on a.Kind = dk.Id and dk.Details = a.Details
	where a.[Table] = @tableId;

	select [!TKind!Array] = null, [Id!!Id] = a.Id, a.[Name], a.[Label],
		[!TTable.Kinds!ParentId] = a.[Details]
	from a2meta.DetailsKinds a 
		inner join @innerTables it on a.Details = it.Id and it.Kind = N'details'
	order by a.[Order];

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
create or alter procedure a2meta.[Operation.Schema]
@Schema sysname,
@Table sysname
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @tableId uniqueidentifier;

	select @tableId = Id from a2meta.[Catalog] r 
	where r.[Schema] = @Schema collate SQL_Latin1_General_CP1_CI_AI
		and r.[Name] = @Table collate SQL_Latin1_General_CP1_CI_AI
		and r.Kind in (N'folder') and IsFolder = 1;

	select [Table!TTable!Object] = null, [!!Id] = c.Id, c.[Schema], c.[Name], c.ItemName, c.ItemsName,
		[Columns!TColumn!Array] = null
	from a2meta.[Catalog] c 
	where c.Id = @tableId;

	select [!TColumn!Array] = null, [Id!!Id] = c.Id, c.[Name], DataType = c.DataType, 
		c.[MaxLength], c.[Role], c.[Order],
		[!TTable.Columns!ParentId] = c.[Table]
	from a2meta.Columns c
	where c.[Table] = @tableId
	order by c.[Order]; -- TableType for operation is not used.
end
go
------------------------------------------------
create or alter procedure a2meta.[Report.Schema]
@Schema sysname,
@Table sysname
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @tableId uniqueidentifier;

	select @tableId = Id from a2meta.[Catalog] r 
	where r.[Schema] = @Schema collate SQL_Latin1_General_CP1_CI_AI
		and r.[Name] = @Table collate SQL_Latin1_General_CP1_CI_AI
		and r.Kind in (N'report');

	select [Table!TTable!Object] = null, [!!Id] = c.Id, c.[Schema], c.[Name], c.ItemName, c.ItemsName,
		c.ItemLabel, c.ItemsLabel, c.[Type],
		[ParentTable.RefSchema!TReference!] = pt.[Schema], [ParentTable.RefTable!TReference] = pt.[Name],
		[ReportItems!TRepItem!Array] = null
	from a2meta.[Catalog] c 
		left join a2meta.[Catalog] pt on c.ParentTable = pt.Id
	where c.Id = @tableId and c.Kind in (N'report');

	select [!TRepItem!Array] = null, ri.Kind, [Column] = c.[Name], c.[DataType],
		[RefSchema] = t.[Schema], [RefTable] = t.[Name],
		ri.[Order], ri.[Label], ri.Checked, ri.[Func],
		[!TTable.ReportItems!ParentId] = ri.[Report]
	from a2meta.ReportItems ri
		inner join a2meta.[Columns] c on ri.[Column] = c.Id
		left join a2meta.[Catalog] t on c.Reference = t.Id
	where ri.Report = @tableId
	order by ri.[Order];
end
go
------------------------------------------------
create or alter procedure a2meta.[Enum.Schema]
@Schema sysname,
@Table sysname
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @tableId uniqueidentifier;

	select @tableId = Id from a2meta.[Catalog] r 
	where r.[Schema] = @Schema collate SQL_Latin1_General_CP1_CI_AI
		and r.[Name] = @Table collate SQL_Latin1_General_CP1_CI_AI
		and r.Kind in (N'enum');

	select [Table!TTable!Object] = null, [!!Id] = c.Id, c.[Schema], c.[Name],
		[EnumValues!TEnumValue!Array] = null
	from a2meta.[Catalog] c 
		left join a2meta.[Catalog] pt on c.ParentTable = pt.Id
	where c.Id = @tableId and c.Kind in (N'enum');

	select [!TEnumValue!Array] = null, [Id] = ei.[Name], [Name] = ei.[Label], ei.[Order],
		ei.Inactive,
		[!TTable.EnumValues!ParentId] = ei.Enum
	from a2meta.EnumItems ei
	where ei.Enum = @tableId
	order by ei.[Order];
end
go
------------------------------------------------
create or alter procedure a2meta.[Table.Form]
@Schema nvarchar(32) = null,
@Table nvarchar(128) = null,
@Id uniqueidentifier = null,
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

	select [Table!TTable!Object] = null, [Id!!Id] = Id, [Name], [Schema], EditWith, 
		ParentTable, ItemLabel, ItemsLabel
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
@Id uniqueidentifier = null,
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
create or alter procedure a2meta.[Catalog.Init]
as
begin
	set nocount on;
	set transaction isolation level read committed;

	if exists(select * from a2meta.[Catalog])
		return;

	declare @cat table([Order] int, IsFolder bit, Parent bigint, [Schema] nvarchar(32), [Name] nvarchar(255), Kind nvarchar(32));
	insert into @cat([Order], IsFolder, [Schema], Kind, [Name]) values

	(10, 0, N'app',  N'app',    N'@[Application]'),
	(11, 1, N'enm',  N'folder', N'@[Enums]'),
	(12, 1, N'cat',  N'folder', N'@[Catalogs]'),
	(13, 1, N'doc',  N'folder', N'@[Documents]'),
	(14, 1, N'op',   N'folder', N'@[Operations]'),
	(15, 1, N'jrn',  N'folder', N'@[Journals]'),
	(16, 1, N'rep',  N'folder', N'@[Reports]'),
	(70, 1, N'ui',   N'folder', N'@[MainMenu]');

	declare @root uniqueidentifier = newid();
	insert into a2meta.[Catalog] (Id, Parent, [Schema], [Kind], [Name], [Order])
	values (@root, @root, N'root', N'root', N'Root', 1);

	insert into a2meta.[Catalog] (Parent, IsFolder, [Schema], [Kind], [Name], [Order])
	select @root, IsFolder, [Schema], [Kind], [Name], [Order] from @cat;

	declare @defCols table([Schema] nvarchar(32), Kind nvarchar(32), [Name] nvarchar(255), 	[DataType] nvarchar(32),
		[MaxLength] int, Ref nvarchar(32), [Role] int, [Order] int, [Required] bit);

	insert into @defCols([Order], [Schema], Kind, [Name], [Role], [Required], DataType, [MaxLength], Ref) values
	-- Catalog
	(1, N'cat', N'table', N'Id',         1, 0, N'id', null, null),
	(2, N'cat', N'table', N'Void',      16, 0, N'bit', null, null),
	(3, N'cat', N'table', N'IsSystem', 128, 0, N'bit', null, null),
	(4, N'cat', N'table', N'Name',       2, 1, N'string',    100, null),
	(5, N'cat', N'table', N'Memo',       0, 0, N'string',    255, null),

	-- Document
	(1, N'doc', N'table', N'Id',         1, 0, N'id',        null, null),
	(2, N'doc', N'table', N'Void',      16, 0, N'bit',       null, null),
	(3, N'doc', N'table', N'Done',     256, 0, N'bit',       null, null),
	(4, N'doc', N'table', N'Date',       0, 0, N'date',      null, null),
	(5, N'doc', N'table', N'Number',  2048, 0, N'string',      32, null),
	(6, N'doc', N'table', N'Operation',  0, 0, N'operation', null, null),
	(7, N'doc', N'table', N'Name',       2, 0, N'string',     100, null), -- todo: computed
	(8, N'doc', N'table', N'Sum',        0, 0, N'money',     null, null),
	(9, N'doc', N'table', N'Memo',       0, 0, N'string',     255, null),
	
	-- cat.Details
	(1, N'cat', N'details', N'Id',      1, 0, N'id',  null, null),
	(2, N'cat', N'details', N'Parent', 32, 0, N'reference', null, N'parent'),
	(3, N'cat', N'details', N'RowNo',   8, 0, N'int', null, null),

	-- doc.Details
	(1, N'doc', N'details', N'Id',       1, 0, N'id',   null, null),
	(2, N'doc', N'details', N'Parent',  32, 0, N'reference',  null, N'parent'),
	(3, N'doc', N'details', N'RowNo',    8, 0, N'int',  null, null),
	(4, N'doc', N'details', N'Kind',   512, 0, N'string', 32, null),
	(5, N'doc', N'details', N'Qty',      0, 1, N'float',null, null),
	(6, N'doc', N'details', N'Price',    0, 1, N'float',null, null),
	(7, N'doc', N'details', N'Sum',      0, 0, N'money',null, null),
	
	-- jrn.Journal
	(1, N'jrn', N'table', N'Id',       1, 0, N'id',       null, null),
	(2, N'jrn', N'table', N'Date',     0, 0, N'datetime', null, null),
	(3, N'jrn', N'table', N'InOut',    0, 0, N'int',      null, null),
	(4, N'jrn', N'table', N'Qty',      0, 0, N'float',    null, null),
	(5, N'jrn', N'table', N'Sum',      0, 0, N'money',    null, null),

	-- op.Operations
	(1, N'op', N'optable', N'Id',       1, 0, N'id',      null, null),
	(2, N'op', N'optable', N'Name',     2, 0, N'string',    64, null),
	(3, N'op', N'optable', N'Url',      0, 0, N'string',   255, null),
	(4, N'op', N'optable', N'Category', 0, 0, N'string',    32, null),
	(5, N'op', N'optable', N'Void',    16, 0, N'bit',     null, null);

	insert into a2meta.DefaultColumns ([Schema], Kind, [Name], DataType, [MaxLength], Ref, [Role], [Order], [Required])
	select [Schema], Kind, [Name], DataType, [MaxLength], Ref, [Role], [Order], [Required]
	from @defCols;

	declare @appId uniqueidentifier;
	select @appId = Id from a2meta.[Catalog] where IsFolder = 0 and Kind = N'app';

	insert into a2meta.[Application] (Id, [Name], Title, IdDataType, [Version])
	values (@appId, N'MyApplication', N'My Application', N'bigint', 1);

	declare @sections table ([Schema] nvarchar(32), [Name] nvarchar(255), [Order] int)
	insert into @sections([Schema], [Name], [Order]) values 
	(N'ui', N'@General', 1),
	(N'ui', N'@Documents', 2),
	(N'ui', N'@Catalogs', 3),
	(N'ui', N'@Reports', 4);

	insert into a2meta.DefaultSections ([Schema], [Name], [Order])
	select [Schema], [Name], [Order] from @sections;

	declare @opTableId uniqueidentifier;
	select @opTableId = Id from a2meta.[Catalog] where [Schema] = N'op' and IsFolder = 1 and Kind = N'folder';

	insert into a2meta.Columns ([Table], [Name], [DataType], [MaxLength], [Role], [Order])
	select @opTableId, [Name], [DataType], [MaxLength], [Role], [Order] from a2meta.DefaultColumns
	where [Schema] = N'op' and Kind = N'optable';

end
go
------------------------------------------------
create or alter procedure a2meta.[App.Metadata]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @appId uniqueidentifier;
	select @appId = Id from a2meta.[Catalog] where [Kind] = N'app' and IsFolder = 0;

	select [Application!TApp!Object] = null, IdDataType, [Name], [Title]
	from a2meta.[Application] where Id = @appId;
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
		when N'acc' then N'Account'
		when N'enm' then N'Enum'
		when N'regi' then N'InfoRegister'
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

	-- FOR DEPLOY
	declare @rootId uniqueidentifier;
	select @rootId = Id from a2meta.[Catalog] where [Kind] = N'root' and Id = [Parent];

	declare @appId uniqueidentifier;
	select @appId = Id from a2meta.[Catalog] where [Kind] = N'app' and IsFolder = 0 and Parent = @rootId;

	select [Application!TApp!Object] = null, [Id!!Id] = @appId, IdDataType, [Name], [Title],
		[Tables!TTable!Array] = null,
		[Operations!TOperation!Array] = null,
		[Enums!TEnum!Array] = null
	from a2meta.[Application] where Id = @appId

	select [!TTable!Array] = null, [Id!!Id] = Id, c.[Schema], c.[Name], c.[Kind], 
		DbName = t.TABLE_NAME, DbSchema = t.TABLE_SCHEMA,
		[Columns!TColumn!Array] = null,
		[!TApp.Tables!ParentId] = @appId
	from a2meta.[Catalog] c
		left join INFORMATION_SCHEMA.TABLES t on t.TABLE_SCHEMA =  c.[Schema] and t.TABLE_NAME = c.[Name]
	where c.[Kind] in (N'table', N'details');

	select [!TColumn!Array] = null, [Id!!Id] = c.Id, c.[Name], c.[DataType], c.[MaxLength], c.[Role],
		[Reference.RefSchema!TRef!] = r.[Schema], [Reference.RefTable!TRef!] = r.[Name],
		DbName = ic.COLUMN_NAME, DbDataType =  ic.DATA_TYPE,
		[!TTable.Columns!ParentId] = c.[Table]
	from a2meta.Columns c
		inner join a2meta.[Catalog] t on c.[Table] = t.Id 
		left join a2meta.[Catalog] r on c.Reference = r.Id
		left join INFORMATION_SCHEMA.COLUMNS ic on ic.TABLE_SCHEMA = t.[Schema] and ic.TABLE_NAME = t.[Name] and ic.COLUMN_NAME = c.[Name]
	where t.Kind <> N'folder'
	order by c.[Order]; -- Used for create TableType

	select 	[!TOperation!Array] = null, [Id] = op.[Name], [Name] = ItemLabel, Category = [Type],
		[!TApp.Operations!ParentId] = @appId
	from a2meta.[Catalog] op
	where [Schema] = N'op' and IsFolder = 0 and Kind = N'operation';

	select 	[!TEnum!Array] = null, [Id!!Id] = e.Id, e.[Name],
		[Values!TEnumValue!Array] = null,
		[!TApp.Enums!ParentId] = @appId
	from a2meta.[Catalog] e
	where [Schema] = N'enm' and IsFolder = 0 and Kind = N'enum';

	select [!TEnumValue!Array] = null, [Id] = [Name], [Name] = [Label], Inactive, [Order],
		[!TEnum.Values!ParentId] = Enum
	from a2meta.EnumItems
	order by [Enum], [Order];
end
go
------------------------------------------------
drop type if exists a2meta.[Operation.TableType];
drop type if exists a2meta.[Enum.TableType];
go
------------------------------------------------
create type a2meta.[Operation.TableType] as table
(
	Id nvarchar(64),
	[Name] nvarchar(255),
	[Url] nvarchar(255),
	Category nvarchar(32)
);
go
------------------------------------------------
create type a2meta.[Enum.TableType] as table
(
	Id nvarchar(16),
	[Name] nvarchar(255),
	[Order] int,
	Inactive bit
);
go
------------------------------------------------
exec a2meta.[Catalog.Init];
go

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


--exec a2meta.[Config.Load] 99

/*
declare @Schema nvarchar(255) = N'doc';
declare @Table nvarchar(255) = N'ClientOrders';

exec a2meta.[Table.Schema] @Schema, @Table;
--exec a2meta.[Config.Load] 99 @Schema, @Table;
*/

--select * from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE where CONSTRAINT_NAME = N'FK_Columns_Parent_Catalog'

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

	-- exetending properties
	-- https://www.mssqltips.com/sqlservertip/5384/working-with-sql-server-extended-properties/
*/


/*
drop table if exists a2meta.[Application]
drop table if exists a2meta.[ApplyMapping]
drop table if exists a2meta.[Apply]
drop table if exists a2meta.[DefaultColumns]
drop table if exists a2meta.[DetailsKinds];
drop table if exists a2meta.[Forms]
drop table if exists a2meta.[Columns]
drop table if exists a2meta.[Items]
drop table if exists a2meta.[Catalog]

select * from a2meta.DefaultSections;

select * from a2meta.ODataTables;
select * from a2meta.ODataColumns order by [Name];
*/


;





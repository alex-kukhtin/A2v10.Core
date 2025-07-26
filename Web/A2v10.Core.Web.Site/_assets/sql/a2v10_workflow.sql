/*
Copyright © 2020-2025 Oleksandr Kukhtin

Last updated : 26 jul 2025
module version : 8233
*/

/* WF TABLES
a2wf.[Versions]
a2wf.InstanceBookmarks
a2wf.InstanceTrack
a2wf.InstanceEvents
a2wf.InstanceVariablesGuid
a2wf.InstanceVariablesString
a2wf.InstanceVariablesInt
a2wf.Instances
a2wf.Workflows
a2wf.[Catalog]
a2wf.AutoStart
-- Custom Table
a2wf.[Inbox]
*/
------------------------------------------------
set nocount on;
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2wf')
	exec sp_executesql N'create schema a2wf authorization dbo';
go
------------------------------------------------
alter authorization on schema::a2wf to dbo;
go
------------------------------------------------
grant execute on schema ::a2wf to public;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'Versions')
create table a2wf.[Versions]
(
	[Module] nvarchar(32) not null,
	[Version] int not null,
	constraint PK_Versions primary key clustered (Module)
);
go
------------------------------------------------
begin
	set nocount on;
	declare @version int;
	set @version = 8233;
	if exists(select * from a2wf.Versions where Module = N'main')
		update a2wf.Versions set [Version] = @version where Module = N'main';
	else
		insert into a2wf.Versions (Module, [Version]) values (N'main', @version);
end
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'CurrentDate')
create table a2wf.[CurrentDate]
(
	[Date] date null
);
go
------------------------------------------------
if not exists(select * from a2wf.CurrentDate)
	insert into a2wf.CurrentDate ([Date]) values (null);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'Catalog')
create table a2wf.[Catalog]
(
	[Id] nvarchar(255) not null,
	[Format] nvarchar(32) not null,
	[Body] nvarchar(max) null,
	[Thumb] varbinary(max) null,
	ThumbFormat nvarchar(32) null,
	[Hash] varbinary(64) null,
	DateCreated datetime not null 
		constraint DF_Catalog_DateCreated default(getutcdate()),
	[Name] nvarchar(255),
	[Memo] nvarchar(255),
	[Svg] nvarchar(max),
	[Key] nvarchar(32),
	constraint PK_Catalog primary key clustered (Id)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2wf' and TABLE_NAME = N'Catalog' and COLUMN_NAME = N'Name')
begin
	alter table a2wf.[Catalog] add [Name] nvarchar(255);
	alter table a2wf.[Catalog] add [Memo] nvarchar(255);
	alter table a2wf.[Catalog] add [Svg] nvarchar(max);
end
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2wf' and TABLE_NAME = N'Catalog' and COLUMN_NAME = N'Key')
	alter table a2wf.[Catalog] add [Key] nvarchar(32);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'Workflows')
create table a2wf.Workflows
(
	[Id] nvarchar(255) not null,
	[Version] int not null,
	[Name] nvarchar(255),
	[Format] nvarchar(32) not null,
	[Text] nvarchar(max) null,
	[Hash] varbinary(64) null,
	[Svg] nvarchar(max) null,
	DateCreated datetime not null constraint DF_Workflows_DateCreated default(getutcdate()),
	constraint PK_Workflows primary key clustered (Id, [Version]) with (fillfactor = 70)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2wf' and TABLE_NAME = N'Workflows' and COLUMN_NAME = N'Name')
	alter table a2wf.Workflows add [Name] nvarchar(255);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2wf' and TABLE_NAME = N'Workflows' and COLUMN_NAME = N'Svg')
	alter table a2wf.Workflows add [Svg] nvarchar(max) null;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'WorkflowArguments')
create table a2wf.WorkflowArguments
(
	[WorkflowId] nvarchar(255) not null,
	[Version] int not null,
	[Name] nvarchar(255) not null,
	[Type] nvarchar(255) null,
	[Value] nvarchar(255) null,
	constraint FK_WorkflowArguments_WorkflowId_Version_Workflows foreign key (WorkflowId, [Version]) references a2wf.Workflows(Id, [Version])
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'Instances')
begin
	create table a2wf.Instances
	(
		Id uniqueidentifier not null 
			constraint PK_Instances primary key clustered with (fillfactor = 70),
		Parent uniqueidentifier null
			constraint FK_Instances_Parent_Workflows foreign key references a2wf.Instances(Id),
		[WorkflowId] nvarchar(255) not null,
		[Version] int not null,
		[State] nvarchar(max) null,
		[ExecutionStatus] nvarchar(255) null,
		DateCreated datetime not null constraint DF_Instances_DateCreated default(getutcdate()),
		DateModified datetime not null constraint DF_Workflows_Modified default(getutcdate()),
		Lock uniqueidentifier null,
		LockDate datetime null,
		CorrelationId nvarchar(255) null,
		constraint FK_Instances_WorkflowId_Workflows foreign key (WorkflowId, [Version]) 
			references a2wf.Workflows(Id, [Version])
	);
	create unique index IDX_Instances_WorkflowId_Id on a2wf.Instances (WorkflowId, Id) with (fillfactor = 70);
end
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'Instances' and COLUMN_NAME=N'CorrelationId')
	alter table a2wf.Instances add CorrelationId nvarchar(255) null;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'InstanceVariablesInt')
begin
	create table a2wf.InstanceVariablesInt
	(
		InstanceId uniqueidentifier not null,
		[Name] nvarchar(255) not null,
		constraint PK_InstanceVariablesInt primary key clustered (InstanceId, [Name]) with (fillfactor = 70),
		WorkflowId nvarchar(255) not null,
		constraint FK_InstanceVariablesInt_PK foreign key ([WorkflowId], InstanceId) references a2wf.Instances (WorkflowId, Id),
		[Value] bigint null
	);
	create index IDX_InstanceVariablesInt_WNV on a2wf.InstanceVariablesInt (WorkflowId, [Name], [Value]) with (fillfactor = 70);
end
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'InstanceVariablesGuid')
begin
	create table a2wf.InstanceVariablesGuid
	(
		InstanceId uniqueidentifier not null,
		[Name] nvarchar(255) not null,
		constraint PK_InstanceVariablesGuid primary key clustered (InstanceId, [Name]) with (fillfactor = 70),
		WorkflowId nvarchar(255) not null,
		constraint FK_InstanceVariablesGuid_PK foreign key ([WorkflowId], InstanceId) references a2wf.Instances (WorkflowId, Id),
		[Value] uniqueidentifier null
	);
	create index IDX_InstanceVariablesGuid_WNV on a2wf.InstanceVariablesGuid (WorkflowId, [Name], [Value]) with (fillfactor = 70);
end
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'InstanceVariablesString')
begin
	create table a2wf.InstanceVariablesString
	(
		InstanceId uniqueidentifier not null,
		[Name] nvarchar(255) not null,
		constraint PK_InstanceVariablesString primary key clustered (InstanceId, [Name]) with (fillfactor = 70),
		WorkflowId nvarchar(255) not null,
		constraint FK_InstanceVariablesString_PK foreign key ([WorkflowId], InstanceId) references a2wf.Instances (WorkflowId, Id),
		[Value] nvarchar(255) null
	);
	create index IDX_InstanceVariablesString_WNV on a2wf.InstanceVariablesString (WorkflowId, [Name], [Value]) with (fillfactor = 70);
end
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'InstanceBookmarks')
begin
	create table a2wf.InstanceBookmarks
	(
		InstanceId uniqueidentifier not null,
		[Bookmark] nvarchar(255) not null,
			constraint PK_InstanceBookmarks primary key clustered (InstanceId, [Bookmark]) with (fillfactor = 70),
		[Activity] nvarchar(255) null,
		[WorkflowId] nvarchar(255) not null,
			constraint FK_InstanceBookmarks_PK foreign key (WorkflowId, InstanceId) references a2wf.Instances (WorkflowId, Id),
	);
	create index IDX_InstanceBookmarks_WB on a2wf.InstanceBookmarks (WorkflowId, Bookmark) with (fillfactor = 70);
end
go
------------------------------------------------
if not exists (select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2wf' and TABLE_NAME=N'InstanceBookmarks' and COLUMN_NAME=N'Activity')
	alter table a2wf.InstanceBookmarks add [Activity] nvarchar(255) null;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'InstanceEvents')
begin
	create table a2wf.InstanceEvents
	(
		InstanceId uniqueidentifier not null,
		Kind nchar(1) not null,
		[Event] nvarchar(255) not null,
			constraint PK_InstanceEvents primary key clustered (InstanceId, Kind, [Event]) with (fillfactor = 70),
		[WorkflowId] nvarchar(255) not null,
		Pending datetime null,
		[Name] nvarchar(255) null, 
		[Text] nvarchar(255) null,
		constraint FK_InstanceEvents_PK foreign key (WorkflowId, InstanceId) references a2wf.Instances (WorkflowId, Id),
	);
	create index IDX_InstanceEvents_WB on a2wf.InstanceEvents (WorkflowId, [Kind], [Event]) with (fillfactor = 70);
end
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'InstanceTrack')
begin
	create table a2wf.InstanceTrack
	(
		Id bigint identity(100, 1) not null
			constraint PK_InstanceTrack primary key clustered,
		InstanceId uniqueidentifier not null,
		RecordNumber int not null,
		EventTime datetime not null
			constraint DF_InstanceTrack_EventTime default(getutcdate()),
		Kind int,
		[Action] int,
		Activity nvarchar(255),
		[Message] nvarchar(max)
	);
	create index IDX_InstanceTrack_InstanceId on a2wf.InstanceTrack (InstanceId) with (fillfactor = 70);
end
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'AutoStart')
create table a2wf.[AutoStart]
(
	[Id] bigint identity(100, 1) not null
		constraint PK_AutoStart primary key clustered,
	[WorkflowId] nvarchar(255) not null,
	[Version] int not null
		constraint DF_AutoStart_Version default(0),
	Params nvarchar(max) null,
	StartAt datetime null,
	Lock uniqueidentifier null,
	DateCreated datetime not null constraint DF_AutoStart_DateCreated default(getutcdate()),
	InstanceId uniqueidentifier null,
	DateStarted datetime null,
	CorrelationId nvarchar(255) null,
	Complete int not null
		constraint DF_AutoStart_Complete default(0)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'AutoStart' and COLUMN_NAME=N'StartAt')
	alter table a2wf.AutoStart add StartAt datetime null;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'AutoStart' and COLUMN_NAME=N'CorrelationId')
	alter table a2wf.AutoStart add CorrelationId nvarchar(255) null;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'AutoStart' and COLUMN_NAME=N'Complete')
	alter table a2wf.AutoStart add Complete int not null 
		constraint DF_AutoStart_Complete default(0) with values;
go
------------------------------------------------
create or alter procedure a2wf.[Catalog.Save]
@UserId bigint = null,
@Id nvarchar(255),
@Body nvarchar(max),
@Format nvarchar(32),
@Key nvarchar(32) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @savedHash varbinary(64);
	declare @newHash varbinary(64);

	begin tran;
		select @savedHash = hashbytes(N'SHA2_256', Body) from a2wf.[Catalog] where Id=@Id;
		select @newHash = hashbytes(N'SHA2_256', @Body);
		if @savedHash is null
			insert into a2wf.[Catalog] (Id, [Format], Body, [Hash], [Key]) 
			values (@Id, @Format, @Body, @newHash, @Key)
		else if @savedHash <> @newHash
			update a2wf.[Catalog] set Body = @Body, [Hash]=@newHash where Id=@Id;
	commit tran;
end
go
------------------------------------------------
create or alter procedure a2wf.[Catalog.Publish]
@UserId bigint = null,
@Id nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @hash varbinary(64);
	declare @version int;
	begin tran;
		select top(1) @hash = [Hash], @version=[Version] 
		from a2wf.Workflows 
		where Id = @Id order by [Version] desc;

		if (select [Hash] from a2wf.[Catalog] where Id = @Id) = @hash
		begin
			select Id, [Version] from a2wf.Workflows where Id = @Id and [Version] = @version;
		end
		else
		begin
			declare @retval table(Id nvarchar(255), [Version] int);
			insert into a2wf.Workflows (Id, [Format], [Text], [Hash], [Name], Svg, [Version])
			output inserted.Id, inserted.[Version] into @retval(Id, [Version])
			select Id, [Format], [Body], [Hash], [Name], Svg, [Version] = 
				(select isnull(max([Version]) + 1, 1) from a2wf.Workflows where Id=@Id)
			from a2wf.[Catalog] where Id=@Id;
			select Id, [Version] from @retval;
		end
	commit tran;
end
go
------------------------------------------------
create or alter procedure a2wf.[Catalog.SaveAndPublish]
@Id nvarchar(255),
@Body nvarchar(max),
@Format nvarchar(32)
as
begin
	set nocount on;
	set transaction isolation level read committed;
	exec a2wf.[Catalog.Save] null, @Id=@Id, @Body=@Body, @Format=@Format;
	exec a2wf.[Catalog.Publish] null, @Id = @Id;
end
go
------------------------------------------------
drop procedure if exists a2wf.[Workflow.SetArguments];
drop type if exists a2wf.[Workflow.Arguments.TableType];
go
------------------------------------------------
create type a2wf.[Workflow.Arguments.TableType] as table(
	[Name] nvarchar(255),
	[Type] nvarchar(255),
	[Value] nvarchar(255)
)
go
------------------------------------------------
create or alter procedure a2wf.[Workflow.SetArguments]
@Id nvarchar(255),
@Version int = 0,
@Rows a2wf.[Workflow.Arguments.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	merge a2wf.WorkflowArguments as t
	using @Rows as s
	on t.WorkflowId = @Id and t.[Version] = @Version and t.[Name] = s.[Name]
	when matched then update set
		t.[Type] = s.[Type],
		t.[Value] = s.[Value]
	when not matched then insert 
		(WorkflowId, [Version], [Name], [Type], [Value]) values
		(@Id, @Version, s.[Name], s.[Type], s.[Value])
	when not matched by source and t.WorkflowId = @Id and t.[Version] = @Version then delete;
end
go
------------------------------------------------
create or alter procedure a2wf.[Workflow.Load]
@UserId bigint = null,
@Id nvarchar(255),
@Version int = 0
as
begin
	set nocount on;
	set transaction isolation level read committed;
	
	select top (1) [Id], [Format], [Version], [Text]
	from a2wf.Workflows
	where Id=@Id and (@Version=0 or [Version]=@Version)
	order by [Version] desc;
end
go
------------------------------------------------
create or alter procedure a2wf.[Catalog.Load]
@UserId bigint = null,
@Id nvarchar(64)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	select c.Body, c.[Format]
	from a2wf.[Catalog] c where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2wf.[Instance.Load]
@UserId bigint = null,
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @inst table(Id uniqueidentifier);

	update a2wf.Instances set Lock=newid(), LockDate = getutcdate()
	output inserted.Id into @inst(Id)
	where Id=@Id and Lock is null;

	select i.Id, [WorkflowId], [Version], [State], ExecutionStatus, Lock, Parent, CorrelationId
	from @inst t inner join a2wf.Instances i on t.Id = i.Id
	where t.Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2wf.[Instance.LoadRaw]
@UserId bigint = null,
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @inst table(Id uniqueidentifier);

	select i.Id, [WorkflowId], [Version], [State], ExecutionStatus, Lock, Parent, CorrelationId
	from a2wf.Instances i
	where i.Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2wf.[Instance.LoadBookmark]
@UserId bigint = null,
@Bookmark nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @inst table(Id uniqueidentifier);

	update top(1) a2wf.Instances set Lock=newid(), LockDate = getutcdate()
	output inserted.Id into @inst(Id)
	from a2wf.Instances i inner join a2wf.InstanceBookmarks b on i.Id = b.InstanceId and i.WorkflowId = b.WorkflowId
	where b.Bookmark=@Bookmark and Lock is null;

	select i.Id, [WorkflowId], [Version], [State], ExecutionStatus, Lock, Parent, CorrelationId
	from @inst t inner join a2wf.Instances i on t.Id = i.Id;
end
go
------------------------------------------------
create or alter procedure a2wf.[Instance.Create]
@UserId bigint = null,
@Id uniqueidentifier,
@Parent uniqueidentifier,
@Version int = 0,
@WorkflowId nvarchar(255),
@ExecutionStatus nvarchar(255),
@CorrelationId nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;
	insert into a2wf.Instances(Id, Parent, WorkflowId, [Version], ExecutionStatus, CorrelationId)
	values (@Id, @Parent, @WorkflowId, @Version, @ExecutionStatus, @CorrelationId);
end
go
------------------------------------------------
drop procedure if exists a2wf.[Instance.Update];
drop type if exists a2wf.[Instance.TableType]
go
------------------------------------------------
create type a2wf.[Instance.TableType] as table
(
	[GUID] uniqueidentifier,
	Id uniqueidentifier,
	WorkflowId nvarchar(255),
	[ExecutionStatus] nvarchar(255) null,
	Lock uniqueidentifier,
	CorrelationId nvarchar(255),
	[State] nvarchar(max)
)
go
------------------------------------------------
drop type if exists a2wf.[Variables.TableType];
go
------------------------------------------------
create type a2wf.[Variables.TableType] as table
(
	[GUID] uniqueidentifier,
	ParentGUID uniqueidentifier
)
go
------------------------------------------------
drop type if exists a2wf.[VariableInt.TableType]
go
------------------------------------------------
create type a2wf.[VariableInt.TableType] as table
(
	ParentGUID uniqueidentifier,
	[Name] nvarchar(255),
	[Value] bigint
)
go
------------------------------------------------
drop type if exists a2wf.[VariableGuid.TableType]
go
------------------------------------------------
create type a2wf.[VariableGuid.TableType] as table
(
	ParentGUID uniqueidentifier,
	[Name] nvarchar(255),
	[Value] uniqueidentifier
)
go
------------------------------------------------
drop type if exists a2wf.[VariableString.TableType];
go
------------------------------------------------
create type a2wf.[VariableString.TableType] as table
(
	ParentGUID uniqueidentifier,
	[Name] nvarchar(255),
	[Value] nvarchar(255)
)
go
------------------------------------------------
drop type if exists a2wf.[InstanceBookmarks.TableType];
go
------------------------------------------------
create type a2wf.[InstanceBookmarks.TableType] as table
(
	ParentGUID uniqueidentifier,
	[Bookmark] nvarchar(255),
	[Activity] nvarchar(255)
)
go
------------------------------------------------
drop type if exists a2wf.[InstanceTrack.TableType]
go
------------------------------------------------
create type a2wf.[InstanceTrack.TableType] as table
(
	ParentGUID uniqueidentifier,
	RecordNumber int,
	EventTime datetime,
	[Kind] int,
	[Action] int,
	Activity nvarchar(255),
	[Message] nvarchar(max)
)
go
------------------------------------------------
drop type if exists a2wf.[InstanceEvent.TableType]
go
------------------------------------------------
create type a2wf.[InstanceEvent.TableType] as table
(
	ParentGUID uniqueidentifier,
	[Event] nvarchar(255),
	Kind nchar(1), /*T(imer)/M(essage)/S(ignal)/E(rror)/esc(A)lation/C(ancel)*/
	Pending datetime,
	[Name] nvarchar(255),
	[Text] nvarchar(255)
)
go
------------------------------------------------
create or alter procedure a2wf.[Instance.Metadata]
as
begin
	declare @Instance a2wf.[Instance.TableType];
	declare @Variables a2wf.[Variables.TableType];
	declare @VariableInt a2wf.[VariableInt.TableType];
	declare @VariableGuid a2wf.[VariableGuid.TableType];
	declare @VariableString a2wf.[VariableString.TableType];
	declare @Bookmarks a2wf.[InstanceBookmarks.TableType];
	declare @TrackRecords a2wf.[InstanceTrack.TableType];
	declare @Events a2wf.[InstanceEvent.TableType];

	select [Instance!Instance!Metadata] = null, * from @Instance;
	select [Variables!Instance.Variables!Metadata] = null, * from @Variables;
	select [IntVariables!Instance.Variables.BigInt!Metadata] = null, * from @VariableInt;
	select [GuidVariables!Instance.Variables.Guid!Metadata] = null, * from @VariableGuid;
	select [StringVariables!Instance.Variables.String!Metadata] = null, * from @VariableString;
	select [Bookmarks!Instance.Bookmarks!Metadata] = null, * from @Bookmarks;
	select [TrackRecords!Instance.TrackRecords!Metadata] = null, * from @TrackRecords;
	select [Events!Instance.Events!Metadata] = null, * from @Events;
end
go
------------------------------------------------
create or alter procedure a2wf.[Instance.Update]
@UserId bigint = null,
@Instance a2wf.[Instance.TableType] readonly,
@Variables a2wf.[Variables.TableType] readonly,
@IntVariables a2wf.[VariableInt.TableType] readonly,
@GuidVariables a2wf.[VariableGuid.TableType] readonly,
@StringVariables a2wf.[VariableString.TableType] readonly,
@Bookmarks a2wf.[InstanceBookmarks.TableType] readonly,
@TrackRecords a2wf.[InstanceTrack.TableType] readonly,
@Events a2wf.[InstanceEvent.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	begin tran;
	
	declare @defuid uniqueidentifier;
	set @defuid = newid();
	declare @rtable table (id uniqueidentifier);
	with ti as (
		select t.Id, t.[State], t.DateModified, t.ExecutionStatus, t.Lock, t.LockDate, t.CorrelationId
		from a2wf.Instances t
		inner join @Instance p on p.Id = t.Id and isnull(t.Lock, @defuid) = isnull(p.Lock, @defuid)
	)
	merge ti as t
	using @Instance as s
	on s.Id = t.Id
	when matched then update set 
		t.[State] = s.[State],
		t.DateModified = getutcdate(),
		t.ExecutionStatus = s.ExecutionStatus,
		t.CorrelationId = isnull(t.CorrelationId, s.CorrelationId),
		t.Lock = null,
		t.LockDate = null
	output inserted.Id into @rtable;
	
	if not exists(select id from @rtable)
	begin
		declare @wfid nvarchar(255);
		select @wfid = cast(Id as nvarchar(255)) from @Instance;
		raiserror(N'Failed to update workflow (id = %s)', 16, -1, @wfid) with nowait;
	end;

	with t as (
		select tt.*
		from a2wf.InstanceVariablesInt tt
		inner join @Instance si on si.Id = tt.InstanceId and si.WorkflowId = tt.WorkflowId
	)
	merge t
	using (
		select si.WorkflowId, InstanceId=si.Id, siv.*
		from @Instance si
		inner join @Variables sv on sv.ParentGUID=si.[GUID]
		inner join @IntVariables siv on siv.ParentGUID=sv.[GUID]
	) as s
	on t.[Name] = s.[Name] and t.InstanceId = s.InstanceId and t.WorkflowId = s.WorkflowId
	when matched then update set 
		t.[Value] = s.[Value]
	when not matched by target then insert
		(InstanceId, [Name], WorkflowId, [Value]) values
		(s.InstanceId, s.[Name], s.WorkflowId, s.[Value])
	when not matched by source then delete;

	with t as (
		select tt.*
		from a2wf.InstanceVariablesGuid tt
		inner join @Instance si on si.Id=tt.InstanceId and si.WorkflowId = tt.WorkflowId
	)
	merge t
	using (
		select si.WorkflowId, InstanceId=si.Id, siv.*
		from @Instance si
		inner join @Variables sv on sv.ParentGUID=si.[GUID]
		inner join @GuidVariables siv on siv.ParentGUID=sv.[GUID]
	) as s
	on t.[Name] = s.[Name] and t.InstanceId = s.InstanceId and t.WorkflowId = s.WorkflowId
	when matched then update set 
		t.[Value] = s.[Value]
	when not matched by target then insert
		(InstanceId, [Name], WorkflowId, [Value]) values
		(s.InstanceId, s.[Name], s.WorkflowId, s.[Value])
	when not matched by source then delete;

	with t as (
		select tt.*
		from a2wf.InstanceVariablesString tt
		inner join @Instance si on si.Id=tt.InstanceId and si.WorkflowId = tt.WorkflowId
	)
	merge t
	using (
		select si.WorkflowId, InstanceId=si.Id, siv.*
		from @Instance si
		inner join @Variables sv on sv.ParentGUID=si.[GUID]
		inner join @StringVariables siv on siv.ParentGUID=sv.[GUID]
	) as s
	on t.[Name] = s.[Name] and t.InstanceId = s.InstanceId and t.WorkflowId = s.WorkflowId
	when matched then update set 
		t.[Value] = s.[Value]
	when not matched by target then insert
		(InstanceId, [Name], WorkflowId, [Value]) values
		(s.InstanceId, s.[Name], s.WorkflowId, s.[Value])
	when not matched by source then delete;

	with t as (
		select tt.*
		from a2wf.InstanceBookmarks tt
		inner join @Instance si on si.Id=tt.InstanceId and tt.WorkflowId = si.WorkflowId
	)
	merge t
	using (
		select si.WorkflowId, InstanceId=si.Id, sib.*
		from @Instance si
		inner join @Bookmarks sib on sib.ParentGUID=si.[GUID]
	) as s
	on t.[Bookmark] = s.[Bookmark] and t.InstanceId = s.InstanceId and t.WorkflowId = s.WorkflowId
	when not matched by target then insert
		(InstanceId, [Bookmark], WorkflowId, Activity) values
		(s.InstanceId, s.[Bookmark], s.WorkflowId, s.Activity)
	when not matched by source then delete;


	with t as (
		select tt.*
		from a2wf.InstanceEvents tt
		inner join @Instance si on si.Id=tt.InstanceId and tt.WorkflowId = si.WorkflowId
	)
	merge t
	using (
		select si.WorkflowId, InstanceId=si.Id, sib.*
		from @Instance si
		inner join @Events sib on sib.ParentGUID=si.[GUID]
	) as s
	on t.[Event] = s.[Event] and t.InstanceId = s.InstanceId and t.WorkflowId = s.WorkflowId and t.Kind = s.Kind
	when matched then update set
		t.Pending = s.Pending,
		t.[Name] = s.[Name],
		t.[Text] = s.[Text]
	when not matched by target then insert
		(InstanceId, [Kind], [Event], WorkflowId, Pending, [Name], [Text]) values
		(s.InstanceId, s.[Kind], s.[Event], s.WorkflowId, Pending, [Name], [Text])
	when not matched by source then delete;

	insert into a2wf.InstanceTrack(InstanceId, RecordNumber, EventTime, [Kind], [Action], [Activity], [Message])
	select i.Id, RecordNumber, EventTime, [Kind], [Action], [Activity], [Message] from 
	@TrackRecords r inner join @Instance i on r.ParentGUID = i.[GUID];

	commit tran;
end
go
------------------------------------------------
create or alter procedure a2wf.[Instance.Exception]
@UserId bigint = null,
@InstanceId uniqueidentifier,
@Action int,
@Kind int,
@Message nvarchar(max)
as
begin
	set nocount on;
	set transaction isolation level read committed;
	insert into a2wf.InstanceTrack(InstanceId, Kind, [Action], [Message], RecordNumber)
	values (@InstanceId, @Kind, @Action, @Message, 0);
end
go
------------------------------------------------
create or alter procedure a2wf.[Engine.Version]
@Module nvarchar(32) = N'main'
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	select [Version] from a2wf.Versions where Module = @Module;
end
go
------------------------------------------------
create or alter function a2wf.[fn_CurrentDate.Get]()
returns datetime
as
begin
	declare @retval datetime;
	select @retval = [Date] from a2wf.CurrentDate;
	if @retval is null
		set @retval = getutcdate();
	else
		set @retval = @retval + cast(cast(getutcdate() as time) as datetime);
	return @retval;
end
go
------------------------------------------------
create or alter procedure a2wf.[Instance.Pending.Load]
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @now datetime;
	set @now = a2wf.[fn_CurrentDate.Get]();
	-- timers
	select [Pending!TPend!Array] = null, InstanceId, EventKey = ev.[Event] , ev.Kind
	from a2wf.InstanceEvents ev
		inner join a2wf.Instances i on ev.InstanceId = i.Id
	where ev.Pending <= @now and ev.Kind=N'T' and i.Lock is null
	order by ev.Pending;

	-- autostart
	declare @AutoStartTable table(Id bigint);
	update a2wf.AutoStart set Lock = newid() 
	output inserted.Id into @AutoStartTable(Id)
	where Lock is null and Complete = 0 and DateStarted is null and 
		(StartAt is null or StartAt <= @now);

	select [AutoStart!TAutoStart!Array] = null, [Id!!Id]= a.Id,  
		WorkflowId, [Version], [Params!!Json] = Params, CorrelationId, a.InstanceId
	from @AutoStartTable t inner join a2wf.AutoStart a on t.Id = a.Id
	order by a.DateCreated;
end
go
------------------------------------------------
create or alter procedure a2wf.[AutoStart.Create]
@WorkflowId nvarchar(255),
@Version int = 0,
@Params nvarchar(max) = null,
@StartAt datetime = null,
@CorrelationId nvarchar(255) = null,
@InstanceId uniqueidentifier = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	insert into a2wf.AutoStart(WorkflowId, [Version], [Params], StartAt, CorrelationId, InstanceId) 
	values (@WorkflowId, @Version, @Params, @StartAt, @CorrelationId, @InstanceId);
end
go
------------------------------------------------
create or alter procedure a2wf.[AutoStart.Complete]
@Id bigint,
@InstanceId uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read committed;
	update a2wf.AutoStart set InstanceId = @InstanceId, DateStarted = getutcdate(), Complete = 1
	where Id=@Id;
end
go
------------------------------------------------
create or alter procedure a2wf.[Instance.CancelChildren]
@InstanceId uniqueidentifier,
@Workflow nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @rtable table(Id uniqueidentifier);
	update a2wf.Instances set ExecutionStatus = N'Canceled' 
	output inserted.Id into @rtable(Id)
	where Parent = @InstanceId and WorkflowId = @Workflow and ExecutionStatus = N'Idle';

	delete from a2wf.InstanceBookmarks 
	from a2wf.InstanceBookmarks ib inner join @rtable t on ib.InstanceId = t.Id and ib.WorkflowId = @Workflow

	delete from a2wf.InstanceEvents 
	from a2wf.InstanceEvents ie inner join @rtable t on ie.InstanceId = t.Id and ie.WorkflowId = @Workflow

	-- remove inbox for all children
	exec sp_executesql N'exec a2wf.[Inbox.CancelChildren] @InstanceId = @InstanceId', 
		N'@InstanceId uniqueidentifier', @InstanceId;
end
go
------------------------------------------------
create or alter procedure a2wf.[Workflow.GetIdByKey]
@Key nvarchar(32)
as
begin
	set nocount on;
	set transaction isolation level read uncommitted

	select top(1) w.Id 
	from a2wf.[Catalog] c inner join a2wf.Workflows w on c.Id = w.Id
	where c.[Key] = @Key;
end
go
------------------------------------------------
create or alter procedure a2wf.[Version.Get]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Version] from a2wf.Versions where Module = N'main';
end
go
------------------------------------------------
create or alter procedure a2wf.[CurrentDate.Get]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select CurrentDate = a2wf.[fn_CurrentDate.Get]();
end
go
------------------------------------------------
create or alter procedure a2wf.[CurrentDate.Set]
@Date date
as
begin
	set nocount on;
	set transaction isolation level read committed;
	update a2wf.CurrentDate set [Date] = @Date;
end
go

-- service procedures
------------------------------------------------
create or alter procedure a2wf.[Instance.Delete]
@TenantId int = 1,
@UserId bigint,
@Id uniqueidentifier = null
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	begin tran;
	delete from a2wf.InstanceEvents where InstanceId = @Id;
	delete from a2wf.InstanceTrack where InstanceId = @Id;
	delete from a2wf.InstanceBookmarks where InstanceId = @Id;
	delete from a2wf.InstanceVariablesGuid where InstanceId = @Id;
	delete from a2wf.InstanceVariablesString where InstanceId = @Id;
	delete from a2wf.InstanceVariablesInt where InstanceId = @Id;
	delete from a2wf.Instances where Id = @Id;
	commit tran;
end
go
/*
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2wf' and TABLE_NAME=N'Inbox')
create table a2wf.[Inbox]
(
	Id uniqueidentifier not null,
	InstanceId uniqueidentifier not null
		constraint FK_Inbox_InstanceId_Instances foreign key references a2wf.Instances(Id),
	Bookmark nvarchar(255) not null,
	Activity nvarchar(255),
	DateCreated datetime not null
		constraint DF_Inbox_DateCreated default(getutcdate()),
	DateRemoved datetime null,
	Void bit not null
		constraint DF_Inbox_Void default(0),
	-- other fields
	constraint PK_Inbox primary key clustered(Id, InstanceId)
);
go
------------------------------------------------
create or alter procedure a2wf.[Instance.Inbox.Create]
@UserId bigint = null,
@Id uniqueidentifier,
@InstanceId uniqueidentifier,
@Bookmark nvarchar(255),
@Activity nvarchar(255)
-- ...other parametets
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	insert into a2wf.[Inbox] (Id, InstanceId, Bookmark, Activity) -- other fields
	values (@Id, @InstanceId, @Bookmark, @Activity); -- other parameters
end
go
------------------------------------------------
create or alter procedure a2wf.[Instance.Inbox.Remove]
@UserId bigint = null,
@Id uniqueidentifier,
@InstanceId uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	update a2wf.Inbox set Void = 1, DateRemoved = getutcdate() where Id=@Id and InstanceId=@InstanceId;
end
go
------------------------------------------------
create or alter procedure a2wf.[Inbox.CancelChildren]
@InstanceId uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2wf.Inbox set Void = 1--, DateRemoved = getutcdate()
	from a2wf.Inbox b inner join a2wf.Instances i on b.InstanceId = i.Id
	where i.Parent = @InstanceId;
end
go
*/


/* _sqlscripts/a2v10_workflow_module.sql */

-- SCHEMAS
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'wfadm')
	exec sp_executesql N'create schema wfadm authorization dbo';
go

grant execute on schema::wfadm to public;
go


-- MIGRATIONS
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2wf' and TABLE_NAME = N'Catalog' and COLUMN_NAME = N'Zoom')
	alter table a2wf.[Catalog] add Zoom float constraint DF_Catalog_Zoom default(1) with values;
go

------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2wf' and TABLE_NAME = N'Catalog' and COLUMN_NAME = N'DateModified')
	alter table a2wf.[Catalog] add DateModified datetime constraint DF_Catalog_DateModified default(getutcdate()) with values;
go

-- INBOX
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
	[User] bigint,
	[Role] bigint,
	[Url] nvarchar(255),
	[Text] nvarchar(255),
	-- other fields
	constraint PK_Inbox primary key clustered(Id, InstanceId)
);
go
------------------------------------------------
if not exists (select * from INFORMATION_SCHEMA.ROUTINES where ROUTINE_SCHEMA=N'a2wf' and ROUTINE_NAME=N'Instance.Inbox.Create')
	exec sp_executesql N'
	create procedure a2wf.[Instance.Inbox.Create]
	@UserId bigint = null,
	@Id uniqueidentifier,
	@InstanceId uniqueidentifier,
	@Bookmark nvarchar(255),
	@Activity nvarchar(255),
	@User bigint,
	@Role bigint,
	@Text nvarchar(255),
	@Url nvarchar(255)
	as
	begin
		set nocount on;
		set transaction isolation level read committed;
		set xact_abort on;

		insert into a2wf.[Inbox] (Id, InstanceId, Bookmark, Activity, [User], [Role], [Text], [Url]) -- other fields
		values (@Id, @InstanceId, @Bookmark, @Activity, @User, @Role, @Text, @Url); -- other parameters
	end
	';
go
------------------------------------------------
if not exists (select * from INFORMATION_SCHEMA.ROUTINES where ROUTINE_SCHEMA=N'a2wf' and ROUTINE_NAME=N'Instance.Inbox.Remove')
	exec sp_executesql N'
	create procedure a2wf.[Instance.Inbox.Remove]
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
	';
go

/*
drop table a2wf.Inbox;
drop procedure a2wf.[Instance.Inbox.Remove];
drop procedure a2wf.[Instance.Inbox.Create];
*/

-- CATALOG
------------------------------------------------
create or alter procedure wfadm.[Catalog.Index]
@UserId bigint,
@Id nvarchar(64) = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @wftable table(Id nvarchar(255), [Version] int);
	insert into @wftable(Id, [Version])
	select [Id], [Version] = max([Version])
	from a2wf.Workflows
	group by Id;

	select [Workflows!TWorkflow!Array] = null, [Id!!Id] = w.Id, w.[Name], t.[Version],
		[DateCreated!!Utc] = w.DateCreated, [DateModified!!Utc] = w.DateModified,
		w.Svg, w.Zoom, w.Memo,
		NeedPublish = cast(case when w.[Hash] = x.[Hash] then 0 else 1 end as bit),
		[Arguments!TArg!Array] = null
	from a2wf.[Catalog] w left join @wftable t on w.Id = t.Id
		left join a2wf.Workflows x on w.Id = x.Id and x.[Version] = t.[Version]
	order by w.DateCreated desc;

	select [!TArg!Array] = null, wa.[Name], wa.[Type], wa.[Value],
		[!TWorkflow.Arguments!ParentId] = t.Id
	from a2wf.WorkflowArguments wa inner join @wftable t on wa.WorkflowId = t.Id and wa.[Version] = t.[Version]
end
go
------------------------------------------------
create or alter procedure wfadm.[Catalog.Load]
@UserId bigint,
@Id nvarchar(64) = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	set @Id = upper(@Id);

	declare @version int, @hash varbinary(64);
	select @version = max([Version]) from a2wf.Workflows where Id = @Id;
	select @hash = Hash from a2wf.Workflows where Id = @Id and [Version] = @version;

	select [Workflow!TWorkflow!Object] = null, [Id!!Id] = Id, [Name!!Name] = [Name], [Body],
		[Svg] = cast(null as nvarchar(max)), [Version] = @version,  Zoom,
		[DateCreated!!Utc] = DateCreated, [DateModified!!Utc] = DateModified,
		NeedPublish = cast(case when [Hash] = @hash then 0 else 1 end as bit)
	from a2wf.[Catalog] 
	where Id = @Id collate SQL_Latin1_General_CP1_CI_AI
	order by Id;
end
go
------------------------------------------------
drop procedure if exists wfadm.[Catalog.Metadata];
drop procedure if exists wfadm.[Catalog.Update];
drop type if exists wfadm.[Catalog.TableType];
go
------------------------------------------------
create type wfadm.[Catalog.TableType] as table
(
	Id nvarchar(64),
	[Name] nvarchar(255),
	[Body] nvarchar(max),
	Svg nvarchar(max),
	Zoom float
);
go
------------------------------------------------
create or alter procedure wfadm.[Catalog.Metadata]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	declare @Workflow wfadm.[Catalog.TableType];
	select [Workflow!Workflow!Metadata] = null, * from @Workflow;
end
go
------------------------------------------------
create or alter procedure wfadm.[Catalog.Update]
@UserId bigint,
@Workflow wfadm.[Catalog.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	declare @rtable table(Id nvarchar(64));
	declare @wfid nvarchar(64);

	merge a2wf.[Catalog] as t
	using @Workflow as s
	on t.Id = s.Id collate SQL_Latin1_General_CP1_CI_AI
	when matched then update set
		t.[Name] = s.[Name],
		t.[Body] = s.[Body],
		t.Svg = s.Svg,
		t.Zoom = round(s.Zoom, 2),
		t.[Hash] = hashbytes(N'SHA2_256', s.Body),
		t.DateModified = getutcdate()
	when not matched then insert 
		(Id, 
			[Name], [Body], Svg, Zoom, [Format], [Hash]) values
		(upper(cast(newid() as nvarchar(64))), 
			s.[Name], s.[Body], s.Svg, round(s.Zoom, 2), N'text/xml', hashbytes(N'SHA2_256', s.Body))
 	output inserted.Id into @rtable(Id);
	select @wfid = Id from @rtable;

	exec wfadm.[Catalog.Load] @UserId = @UserId, @Id = @wfid;
end
go

------------------------------------------------
create or alter procedure wfadm.[Workflow.Fetch]
@UserId bigint,
@Text nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	
	declare @fr nvarchar(255) = N'%' + @Text + N'%';
	select [Workflows!TWorkflow!Array] = null, [Id!!Id] = c.Id, [Name!!Name] = c.[Name]
	from a2wf.[Catalog] c
	where (@fr is null or c.[Name] like @fr)
		and c.Id in (select Id from a2wf.Workflows);
end
go
------------------------------------------------
create or alter procedure wfadm.[Workflow.Download.Load]
@UserId bigint,
@Id uniqueidentifier,
@Key nvarchar(64) = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Name] = [Name] + N'.bpmn', Mime =  [Format], 
		[Data] = Body, SkipToken = cast(1 as bit)
	from a2wf.[Catalog] where Id = @Id;
end
go
------------------------------------------------
create or alter procedure wfadm.[Workflow.Upload.Update] 
@UserId bigint,
@Id uniqueidentifier = null,
@Stream varbinary(max),
@Name nvarchar(255),
@Mime nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	-- convert varbinary to nvarchar (UTF8)
	if right(@Name, 5) <> N'.bpmn'
		throw 60000, N'UI:Only *.bpmn files are supported', 0;

	declare @tmp table(val varchar(max) collate LATIN1_GENERAL_100_CI_AS_SC_UTF8);
	insert into @tmp(val) values (@Stream);

	declare @xml nvarchar(max);
	select @xml = val from @tmp;

	declare @wfid uniqueidentifier = newid();
	declare @idstr nvarchar(255) = upper(cast(@wfid as nvarchar(255)));

	insert into a2wf.[Catalog](Id, [Name], [Body], [Format], [Hash]) values
		(@idstr, replace(@Name, N'.bpmn', N''), 
		@xml, N'text/xml', hashbytes(N'SHA2_256', @xml));

	select [Result!TResult!Object] = null, [Id] = @idstr;
end
go
------------------------------------------------
create or alter procedure wfadm.[Workflow.Delete]
@UserId bigint,
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	if exists(select * from a2wf.Workflows where Id = @Id)
		throw 60000, N'UI:@[WfAdm.Error.WorkflowUsed]', 0;
	delete from a2wf.[Catalog] where Id = @Id;
end
go

-- INSTANCE
------------------------------------------------
create or alter procedure wfadm.[Instance.Index]
@UserId bigint,
@Id nvarchar(64) = null,
@Offset int = 0,
@PageSize int = 20,
@Order nvarchar(255) = N'datemodified',
@Dir nvarchar(20) = N'desc',
@Workflow nvarchar(255) = null,
@State nvarchar(32) = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	set @Order = lower(@Order);
	set @Dir = lower(@Dir);
	set @Workflow = upper(@Workflow);

	set @State = nullif(@State, N'');

	declare @inst table (Id uniqueidentifier, rowno int identity(1,1), rowcnt int);

	insert into @inst(Id, rowcnt)
	select i.Id,
		count(*) over()
	from a2wf.Instances i
		inner join a2wf.[Workflows] w on i.WorkflowId = w.Id and i.[Version] = w.[Version]
	where (@Workflow is null or w.Id = @Workflow)
		and (@State is null or i.[ExecutionStatus] = @State)
	order by 
		case when @Dir = N'asc' then
			case @Order 
				when N'id' then i.[Id]
			end
		end asc,
		case when @Dir = N'asc' then
			case @Order 
				when N'datecreated' then i.DateCreated
				when N'datemodified' then i.DateModified
			end
		end asc,
		case when @Dir = N'asc' then
			case @Order 
				when N'ExecutionStatus' then i.ExecutionStatus
			end
		end asc,
		case when @Dir = N'desc' then
			case @Order
				when N'id' then i.[Id]
			end
		end desc,
		case when @Dir = N'desc' then
			case @Order
				when N'datecreated' then i.DateCreated
				when N'datemodified' then i.DateModified
			end
		end desc,
		case when @Dir = N'desc' then
			case @Order
				when N'ExecutionStatus' then i.ExecutionStatus
			end
		end desc
	offset @Offset rows fetch next @PageSize rows only
	option (recompile);

	select [Instances!TInstance!Array] = null, [Id!!Id] = i.Id, w.[Name], i.[Version],
		i.ExecutionStatus, Lock, [LockDate!!Utc] = LockDate, i.CorrelationId,
		[DateCreated!!Utc] = i.DateCreated, [DateModified!!Utc] = i.DateModified,
		[Inboxes!TInbox!Array] = null, [Bookmarks!TBookmark!Array] = null,
		[!!RowCount] = t.rowcnt
	from a2wf.Instances i inner join @inst t on i.Id = t.Id
		inner join a2wf.[Workflows] w on i.WorkflowId = w.Id and i.[Version] = w.[Version]
	order by t.rowno;

	-- Inbox MUST be created
	select [!TInbox!Array] = null, [Id!!Id] = i.Id, 
		Bookmark, [DateCreated!!Utc] = DateCreated, i.[Text], i.[User], i.[Role], i.[Url],
		[Instance!TInstance.Inboxes!ParentId] = InstanceId
	from a2wf.Inbox i inner join @inst t on i.InstanceId = t.Id
	where i.Void = 0;

	select [!TBookmark!Array] = null, i.Bookmark,
		[Instance!TInstance.Bookmarks!ParentId] = InstanceId
	from a2wf.InstanceBookmarks i inner join @inst t on i.InstanceId = t.Id;

	select [Answer!TAnswer!Object] = null, [Answer] = cast(null as nvarchar(255));

	select [!TWorkflow!Map] = null, [Id!!Id] = Id, [Name!!Name] = [Name]
	from a2wf.Workflows
	where Id = @Workflow;

	select [StartWorkflow!TStartWF!Object] = null, [Id!!Id] = Id,
		[Arguments!TArg!Array] = null
	from a2wf.[Catalog] where 0 <> 0;

	select [!TArg!Array] = null, wa.[Name], wa.[Type], wa.[Value],
		[!TStartWF.Arguments!ParentId] = wa.WorkflowId
	from a2wf.WorkflowArguments wa
	where 0 <> 0;

	select [!$System!] = null, [!Instances!Offset] = @Offset, [!Instances!PageSize] = @PageSize, 
		[!Instances!SortOrder] = @Order, [!Instances!SortDir] = @Dir,
		[!Instances.Workflow.TWorkflow.RefId!Filter] = @Workflow, 
		[!Instances.State!Filter] = isnull(@State, N'');
end
go
------------------------------------------------
create or alter procedure wfadm.[Instance.Delete]
@UserId bigint,
@Id uniqueidentifier = null
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;
	begin tran;
	
	delete from a2wf.Inbox where InstanceId = @Id;
	exec a2wf.[Instance.Delete] @UserId = @UserId, @Id = @Id;

	commit tran;
end
go

------------------------------------------------
create or alter procedure wfadm.[Instance.Show.Load]
@UserId bigint,
@Id nvarchar(64) = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Instance!TInstance!Object] = null, [Id!!Id] = i.Id, WorkflowId = i.WorkflowId, 
		[Version] = i.[Version], [Xml] = w.[Text], [DateModified!!Utc] = DateModified,
		[Track!TTrack!Array] = null, [UserTrack!TUserTrack!Array] = null,
		[FullTrack!TFullTrack!Array] = null,
		i.ExecutionStatus
	from a2wf.Instances i inner join a2wf.Workflows w on i.WorkflowId = w.Id and i.[Version] = w.[Version]
	where i.Id = @Id;

	with TE as(
		select InstanceId, Activity
		from a2wf.InstanceTrack where InstanceId = @Id
			and [Kind] = 0 /*activity*/ and [Action] in (
				1 /*execute*/, 7 /*inbox*/, 4 /* Event */)
		group by InstanceId, Activity
	)
	select [!TTrack!Array] = null, TE.Activity,
		IsIdle = cast(
			case when b.Bookmark is not null or e.[Event] is not null
			then 1 else 0 end as bit
		),
		[!TInstance.Track!ParentId] = TE.InstanceId 
	from TE
		left join a2wf.InstanceBookmarks b on TE.InstanceId = b.InstanceId and TE.Activity = b.Activity
		left join a2wf.InstanceEvents e on TE.InstanceId = e.InstanceId and TE.Activity = e.[Event];

	select [!TFullTrack!Array] = null, Id = Activity, [Action], [EventTime!!Utc] = EventTime,
		[Message],
		[!TInstance.FullTrack!ParentId] = InstanceId 
	from a2wf.InstanceTrack where InstanceId = @Id and [Action] <> 0 and Kind = 0 /*Activity*/
	order by [EventTime] desc, RecordNumber desc;

end
go
------------------------------------------------
create or alter procedure wfadm.[Instance.Log.Load]
@UserId bigint,
@Id nvarchar(64) = null,
@Offset int = 0,
@PageSize int = 20
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Records!TRecord!Array] = null, [Id!!Id] = t.Id, t.Activity, t.[Message],
		[EventTime!!Utc] = t.EventTime, t.Kind, t.RecordNumber, t.[Action],
		[!!RowCount] = count(*) over()
	from a2wf.InstanceTrack t
	where t.InstanceId = @Id and [Action] <> 0 -- skip start
	order by Id desc
	offset @Offset rows fetch next @PageSize rows only
	option (recompile);

	select [!$System!] = null, [!Records!Offset] = @Offset, [!Records!PageSize] = @PageSize;
end
go

------------------------------------------------
create or alter procedure wfadm.[Instance.Events.Load]
@UserId bigint,
@Id nvarchar(64) = null,
@Offset int = 0,
@PageSize int = 20
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Events!TEvent!Array] = null, e.Kind, e.[Event], e.[Name], e.[Text],
		[Pending!!Utc] = e.Pending
	from a2wf.InstanceEvents e
	where e.InstanceId = @Id
	order by e.Pending;
end
go
------------------------------------------------
create or alter procedure wfadm.[Instance.Variables.Load]
@UserId bigint,
@Id nvarchar(64) = null,
@Offset int = 0,
@PageSize int = 20
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Instance!TInstance!Object] = null, [Id!!Id] = i.Id, 
		[State!!Json] = [State],
		Variables = cast(null as nvarchar(max))
	from a2wf.Instances i
	where i.Id = @Id;
end
go
------------------------------------------------
create or alter procedure wfadm.[Instance.Unlock]
@UserId bigint,
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2wf.Instances set Lock = null, LockDate = null where Id = @Id;
end
go
-- AUTOSTART
------------------------------------------------
create or alter procedure wfadm.[AutoStart.Index]
@UserId bigint,
@Id uniqueidentifier = null,
@Offset int = 0,
@PageSize int = 20,
@Order nvarchar(255) = N'id',
@Dir nvarchar(20) = N'desc'
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	set @Order = lower(@Order);
	set @Dir = lower(@Dir);

	declare @inst table (Id bigint, rowno int identity(1,1), rowcnt int, [version] int);

	insert into @inst(Id, [version], rowcnt)
	select a.Id, a.[Version],
		count(*) over()
	from a2wf.AutoStart a
	order by 
		case when @Dir = N'asc' then
			case @Order 
				when N'id' then a.[Id]
			end
		end asc,
		case when @Dir = N'asc' then
			case @Order 
				when N'datecreated' then a.DateCreated
				when N'datestarted' then a.DateStarted
			end
		end asc,
		case when @Dir = N'desc' then
			case @Order
				when N'id' then a.[Id]
			end
		end desc,
		case when @Dir = N'desc' then
			case @Order
				when N'datecreated' then a.DateCreated
				when N'datestarted' then a.DateStarted
			end
		end desc,
		a.Id
	offset @Offset rows fetch next @PageSize rows only
	option (recompile);

	select [AutoStart!TAutoStart!Array] = null, [Id!!Id] = a.Id, a.[Version],
		[StartAt!!Utc] = a.StartAt, a.Lock, a.InstanceId, a.CorrelationId,
		[DateCreated!!Utc] = a.DateCreated, [DateStarted!!Utc] = a.DateStarted,
		[WorkflowName] = c.[Name],
		[!!RowCount] = t.rowcnt
	from a2wf.AutoStart a inner join @inst t on a.Id = t.Id
		left join a2wf.[Catalog] c on a.WorkflowId = c.Id
	order by t.rowno;

	select [!$System!] = null, [!AutoStart!Offset] = @Offset, [!AutoStart!PageSize] = @PageSize, 
		[!AutoStart!SortOrder] = @Order, [!AutoStart!SortDir] = @Dir;
end
go
------------------------------------------------
create or alter procedure wfadm.[AutoStart.Load]
@UserId bigint,
@Id bigint = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [AutoStart!TAutoStart!Object] = null, [Id!!Id] = a.Id, a.[Version],
		[StartAt!!Utc] = StartAt, CorrelationId, a.Params, 
		[Workflow!TWorkflow!RefId] = a.WorkflowId,
		[DateCreated!!Utc] = a.DateCreated, [DateStarted!!Utc] = a.DateStarted,
		[WorkflowName] = c.[Name]
	from a2wf.AutoStart a
		inner join a2wf.[Catalog] c on a.WorkflowId = c.Id
	where a.Id = @Id;

	select [!TWorkflow!Map] = null, [Id!!Id] = c.Id, [Name!!Name] = c.[Name]
	from a2wf.[Catalog] c
		inner join a2wf.AutoStart a on a.WorkflowId = c.[Id]
	where a.Id = @Id;
end
go
------------------------------------------------
drop procedure if exists wfadm.[AutoStart.Metadata];
drop procedure if exists wfadm.[AutoStart.Update];
drop type if exists wfadm.[AutoStart.TableType];
go
------------------------------------------------
create type wfadm.[AutoStart.TableType] as table (
	Workflow nvarchar(255),
	StartAt datetime,
	TimezoneOffset int,
	CorrelationId nvarchar(255)
);
go
------------------------------------------------
create or alter procedure wfadm.[AutoStart.Metadata]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @AutoStart wfadm.[AutoStart.TableType];
	select [AutoStart!AutoStart!Metadata] = null, * from @AutoStart;
end
go
------------------------------------------------
create or alter procedure wfadm.[AutoStart.Update]
@UserId bigint,
@AutoStart wfadm.[AutoStart.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;

	-- SQL server time may be UTC!!!

	declare @rtable table(Id bigint);
	insert into a2wf.AutoStart (WorkflowId, CorrelationId, StartAt)
	output inserted.Id into @rtable(Id)
	select upper(Workflow), CorrelationId, dateadd(minute, TimezoneOffset, StartAt)
	from @AutoStart;

	declare @Id bigint;
	select @Id = Id from @rtable;
	exec wfadm.[AutoStart.Load] @UserId, @Id;
end
go


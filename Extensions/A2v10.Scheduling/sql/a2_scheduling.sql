/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 21 aug 2023
module version : 8135
*/

/*
-- json sample
declare @json nvarchar(max) =
(select * from 
	(select Id = 1, [Text] = N'Text', [Uid] = newid(), [Date] = getutcdate(), Number = 2934.472) j
	for json auto
);
print @json;
*/
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2sch')
	exec sp_executesql N'create schema a2sch';
go
------------------------------------------------
grant execute on schema ::a2sch to public;
go

------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2sch' and SEQUENCE_NAME=N'SQ_Commands')
	create sequence a2sch.SQ_Commands as bigint start with 100 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2sch' and TABLE_NAME=N'Commands')
create table a2sch.Commands
(
	Id	bigint not null constraint PK_Commands primary key
		constraint DF_Commands_PK default(next value for a2sch.SQ_Commands),
	Command nvarchar(64) not null,
	[Data] nvarchar(max) null,
	[Complete] int not null,
	UtcRunAt datetime null,
	Lock uniqueidentifier null,
	LockDate datetime null,
	[UtcDateCreated] datetime not null
		constraint DF_Commands_UtcDateCreated default(getutcdate()),
	[UtcDateComplete] datetime null,
	Error nvarchar(1024) sparse null 
);
go

------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2sch' and TABLE_NAME=N'Exceptions')
create table a2sch.Exceptions
(
	Id bigint identity(100, 1) not null constraint PK_Exceptions primary key,
	[JobId] nvarchar(64) null,
	[Message] nvarchar(255) null,
	[UtcDateCreated] datetime not null
		constraint DF_Exceptions_UtcDateCreated default(getutcdate())
);
go
------------------------------------------------
create or alter procedure a2sch.[Command.List]
@Limit int = 10
as
begin
	set nocount on;
	set transaction isolation level read committed;
	
	declare @inst table(Id bigint);

	update top(@Limit) a2sch.Commands set Lock = newid(), LockDate = getutcdate()
	output inserted.Id into @inst
	where Lock is null and Complete = 0 and 
		(UtcRunAt is null or UtcRunAt < getutcdate());

	select b.Id, b.Command, b.[Data], b.Lock
	from @inst t inner join a2sch.Commands b on t.Id = b.Id
	order by b.Id; -- required!
end
go
------------------------------------------------
create or alter procedure a2sch.[Command.Complete]
@Id bigint,
@Lock uniqueidentifier,
@Complete int,
@Error nvarchar(1024) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2sch.Commands set Complete = case when @Complete = 1 then 1 else -1 end, 
		Error = @Error, UtcDateComplete = getutcdate()
	where Id = @Id and Lock = @Lock;
end
go
------------------------------------------------
create or alter procedure a2sch.[Exception]
@Message nvarchar(255),
@JobId nvarchar(64)
as
begin
	set nocount on;
	set transaction isolation level read committed;
	insert into a2sch.Exceptions([JobId], [Message]) values (@JobId, @Message);
end
go
------------------------------------------------
drop procedure if exists a2sch.[Command.Collection.Queue];
drop type if exists a2sch.[Command.TableType];
go
------------------------------------------------
create type a2sch.[Command.TableType] as table
(
	Command nvarchar(64),
    [Data] nvarchar(max),
	UtcRunAt datetime
)
go
------------------------------------------------
create or alter procedure a2sch.[Command.Queue]
@Command nvarchar(64),
@Data nvarchar(1024) = null,
@UtcRunAt datetime = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @rtable table(id bigint);
	insert a2sch.Commands (Command, UtcRunAt, [Data], Complete)
	output inserted.Id into @rtable(id)
	values (@Command, @UtcRunAt, @Data, 0);

	select [Id] = id from @rtable;
end
go
------------------------------------------------
create or alter procedure a2sch.[Command.Collection.Queue]
@Commands a2sch.[Command.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	insert a2sch.Commands (Command, [Data], UtcRunAt, Complete)
	select Command, [Data], UtcRunAt, 0
	from @Commands;
end
go

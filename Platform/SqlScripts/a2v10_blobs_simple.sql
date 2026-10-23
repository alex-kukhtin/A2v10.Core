/*
Copyright © 2026 Oleksandr Kukhtin

Last updated : 23 oct 2026
module version : 8672
*/
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2blob')
	exec sp_executesql N'create schema a2blob authorization dbo';
go
------------------------------------------------
grant execute on schema ::a2blob to public;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2blob' and SEQUENCE_NAME=N'SQ_Blobs')
	create sequence a2blob.SQ_Blobs as bigint start with 1000 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2blob' and TABLE_NAME=N'Blobs')
create table a2blob.Blobs
(
	Id bigint not null
		constraint PK_Blobs primary key
		constraint DF_Blobs_PK default(next value for a2blob.SQ_Blobs),
	[Name] nvarchar(255) not null,
	Mime nvarchar(255) not null,
	BlobName nvarchar(1024) not null,
	ThumbName nvarchar(1024) null,
	[Hash] varbinary(32) null,
	AccessToken uniqueidentifier not null constraint DF_Blobs_AccessToken default(newid()),
	UserId bigint not null
		constraint FK_Blobs_UserId_Users foreign key references a2security.Users(Id),
	UtcDateCreated datetime not null constraint DF_Blobs_UtcDateCreated default(getutcdate())
);
go
------------------------------------------------
-- SHA-256 of the stored bytes, to find duplicates; null in rows written before it
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2blob' and TABLE_NAME = N'Blobs' and COLUMN_NAME = N'Hash')
	alter table a2blob.Blobs add [Hash] varbinary(32) null;
go
------------------------------------------------
if not exists(select * from sys.indexes where [object_id] = object_id(N'a2blob.Blobs') and [name] = N'IX_Blobs_Hash')
	create nonclustered index IX_Blobs_Hash on a2blob.Blobs([Hash]);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2blob' and SEQUENCE_NAME=N'SQ_BlobLinks')
	create sequence a2blob.SQ_BlobLinks as bigint start with 1000 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2blob' and TABLE_NAME=N'BlobLinks')
create table a2blob.BlobLinks
(
	Id bigint not null
		constraint PK_BlobLinks primary key
		constraint DF_BlobLinks_PK default(next value for a2blob.SQ_BlobLinks),
	[Base] nvarchar(255) not null,
	LinkId platformid not null,
	[Blob] bigint not null
		constraint FK_BlobLinks_Blob_Blobs foreign key references a2blob.Blobs(Id),
	constraint UNQ_BlobLinks unique ([Base], LinkId, [Blob])
);
go
------------------------------------------------
create or alter procedure a2blob.[Blob.Insert]
@UserId bigint,
@Name nvarchar(255),
@Mime nvarchar(255),
@BlobName nvarchar(1024),
@Hash varbinary(32),
@ThumbName nvarchar(1024) = null,
@Base nvarchar(255) = null,
@LinkId platformid = null
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	declare @blobs table(Id bigint, AccessToken uniqueidentifier);
	declare @links table(Id bigint);

	begin tran;

	insert into a2blob.Blobs([Name], Mime, BlobName, ThumbName, [Hash], UserId)
	output inserted.Id, inserted.AccessToken into @blobs(Id, AccessToken)
	values (@Name, @Mime, @BlobName, @ThumbName, @Hash, @UserId);

	-- an attachment: the link in the same transaction, never a blob linked by half
	if @Base is not null
		insert into a2blob.BlobLinks([Base], LinkId, [Blob])
		output inserted.Id into @links(Id)
		select @Base, @LinkId, Id from @blobs;

	commit tran;

	select b.Id, b.AccessToken, Link = (select top(1) Id from @links)
	from @blobs b;
end
go
------------------------------------------------
create or alter procedure a2blob.[Blob.Load]
@Id bigint
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Name], Mime, BlobName, ThumbName, [Hash], AccessToken
	from a2blob.Blobs where Id = @Id;
end
go

/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 24 may 2023
module version : 8100
*/
------------------------------------------------
create or alter procedure a2ui.[Menu.User.Load]
@TenantId int = 1,
@UserId bigint
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select * from a2security.ViewUsers where Id = @UserId;
end
go
------------------------------------------------
drop procedure if exists a2ui.[Menu.Merge];
drop type if exists a2ui.[Menu.TableType]
go
------------------------------------------------
create type a2ui.[Menu.TableType] as table
(
	Id uniqueidentifier,
	Parent uniqueidentifier,
	[Name] nvarchar(255),
	[Url] nvarchar(255),
	Icon nvarchar(255),
	[Order] int,
	ClassName nvarchar(255),
	CreateName nvarchar(255),
	CreateUrl nvarchar(255)
);
go

------------------------------------------------
create or alter procedure a2ui.[Menu.Merge]
@TenantId int,
@Menu a2ui.[Menu.TableType] readonly,
@ModuleId uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read committed;

	with T as (
		select * from a2ui.Menu where TenantId = @TenantId
	)
	merge T as t
	using @Menu as s
	on t.Id = s.Id and t.TenantId = @TenantId
	when matched then update set
		t.Id = s.Id,
		t.Parent = s.Parent,
		t.[Name] = s.[Name],
		t.[Url] = s.[Url],
		t.[Icon] = s.Icon,
		t.[Order] = s.[Order],
		t.ClassName = s.ClassName,
		t.CreateUrl= s.CreateUrl,
		t.CreateName = s.CreateName
	when not matched by target then insert(Module, TenantId, Id, Parent, [Name], [Url], Icon, [Order], ClassName, CreateUrl, CreateName) values 
		(@ModuleId, @TenantId, Id, Parent, [Name], [Url], Icon, [Order], ClassName, CreateUrl, CreateName)
	when not matched by source and t.TenantId = @TenantId and t.Module = @ModuleId then
		delete;
end
go

------------------------------------------------
create or alter procedure a2ui.[Menu.User.Load]
@TenantId int = 1,
@UserId bigint = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @RootId uniqueidentifier = N'00000000-0000-0000-0000-000000000000';
	with RT as (
		select Id=m0.Id, ParentId = m0.Parent, [Level] = 0
			from a2ui.Menu m0
			where m0.TenantId = @TenantId and m0.Id = @RootId
		union all
		select m1.Id, m1.Parent, RT.[Level]+1
			from RT inner join a2ui.Menu m1 on m1.Parent = RT.Id and m1.TenantId = @TenantId
	)
	select [Menu!TMenu!Tree] = null, [Id!!Id]=RT.Id, [!TMenu.Menu!ParentId]=RT.ParentId,
		[Menu!TMenu!Array] = null,
		m.[Name], m.Url, m.Icon, m.ClassName, m.CreateUrl, m.CreateName
	from RT 
		inner join a2ui.Menu m on m.TenantId = @TenantId and RT.Id=m.Id
	order by RT.[Level], m.[Order], RT.[Id];

	-- system parameters
	select [SysParams!TParam!Object]= null, [AppTitle], [AppSubTitle]
	from (select [Name], [Value]=StringValue from a2sys.SysParams) as s
		pivot (min([Value]) for [Name] in ([AppTitle], [AppSubTitle])) as p;
end
go

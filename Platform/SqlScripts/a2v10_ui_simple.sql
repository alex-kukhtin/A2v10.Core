/*
Copyright © 2008-2025 Oleksandr Kukhtin

Last updated : 31 may 2025
module version : 8553
*/
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
	CreateUrl nvarchar(255),
	IsDevelopment bit
);
go
------------------------------------------------
create or alter procedure a2ui.[Menu.Merge]
@Menu a2ui.[Menu.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;

	merge a2ui.Menu as t
	using @Menu as s
	on t.Id = s.Id
	when matched then update set
		t.Id = s.Id,
		t.Parent = s.Parent,
		t.[Name] = s.[Name],
		t.[Url] = s.[Url],
		t.[Icon] = s.Icon,
		t.[Order] = s.[Order],
		t.ClassName = s.ClassName,
		t.CreateUrl= s.CreateUrl,
		t.CreateName = s.CreateName,
		t.IsDevelopment = isnull(s.IsDevelopment, 0)
	when not matched by target then insert(Id, Parent, [Name], [Url], Icon, [Order], ClassName, CreateUrl, CreateName, IsDevelopment) values 
		(Id, Parent, [Name], [Url], Icon, [Order], ClassName, CreateUrl, CreateName, isnull(IsDevelopment, 0))
	when not matched by source then delete;
end
go
------------------------------------------------
create or alter procedure a2ui.[Menu.User.Load]
@UserId bigint = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @isDevelopment bit = 0;

	declare @RootId uniqueidentifier = N'00000000-0000-0000-0000-000000000000';
	declare @RootMobileId uniqueidentifier = N'00000007-0007-0007-0007-000000000007';
	with RT as (
		select Id=m0.Id, ParentId = m0.Parent, [Level] = 0
			from a2ui.Menu m0
			where m0.Id = @RootId
		union all
		select m1.Id, m1.Parent, RT.[Level]+1
			from RT inner join a2ui.Menu m1 on m1.Parent = RT.Id
	)
	select [Menu!TMenu!Tree] = null, [Id!!Id]=RT.Id, [!TMenu.Menu!ParentId]=RT.ParentId,
		[Menu!TMenu!Array] = null,
		m.[Name], m.[Url], m.Icon, m.ClassName, m.CreateUrl, m.CreateName
	from RT 
		inner join a2ui.Menu m on RT.Id=m.Id
	where IsDevelopment = 0 or IsDevelopment is null or IsDevelopment = @isDevelopment
	order by RT.[Level], m.[Order], RT.[Id];

	-- system parameters
	select [SysParams!TParam!Object]= null, [AppTitle], [AppSubTitle]
	from (select [Name], [Value]=StringValue from a2sys.SysParams) as s
		pivot (min([Value]) for [Name] in ([AppTitle], [AppSubTitle])) as p;
end
go
------------------------------------------------
create or alter procedure a2ui.[MenuSP.User.Load]
@UserId bigint = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	-- system parameters
	select [SysParams!TParam!Object]= null, [AppTitle], [AppSubTitle]
	from (select [Name], [Value]=StringValue from a2sys.SysParams) as s
		pivot (min([Value]) for [Name] in ([AppTitle], [AppSubTitle])) as p;
end
go

/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 06 sep 2023
module version : 8152
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
@TenantId int,
@Menu a2ui.[Menu.TableType] readonly,
@ModuleId uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read committed;

	with T as (
		select * from a2ui.Menu where Tenant = @TenantId
	)
	merge T as t
	using @Menu as s
	on t.Id = s.Id and t.Tenant = @TenantId
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
	when not matched by target then insert(Module, Tenant, Id, Parent, [Name], [Url], Icon, [Order], ClassName, CreateUrl, CreateName) values 
		(@ModuleId, @TenantId, Id, Parent, [Name], [Url], Icon, [Order], ClassName, CreateUrl, CreateName)
	when not matched by source and t.Tenant = @TenantId and t.Module = @ModuleId then
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

	declare @isDevelopment bit;
	select @isDevelopment = IsDevelopment from a2sys.Applications where TenantId = @TenantId and Id = 1;
	set @isDevelopment = isnull(@isDevelopment, 0);

	set @TenantId = 1; -- TODO: TEMPORARY!!!!

	declare @RootId uniqueidentifier = N'00000000-0000-0000-0000-000000000000';
	with RT as (
		select Id=m0.Id, ParentId = m0.Parent, [Level] = 0
			from a2ui.Menu m0
			where m0.Tenant = @TenantId and m0.Id = @RootId
		union all
		select m1.Id, m1.Parent, RT.[Level]+1
			from RT inner join a2ui.Menu m1 on m1.Parent = RT.Id and m1.Tenant = @TenantId
	)
	select [Menu!TMenu!Tree] = null, [Id!!Id]=RT.Id, [!TMenu.Menu!ParentId]=RT.ParentId,
		[Menu!TMenu!Array] = null,
		m.[Name], m.Url, m.Icon, m.ClassName, m.CreateUrl, m.CreateName
	from RT 
		inner join a2ui.Menu m on m.Tenant = @TenantId and RT.Id=m.Id
	where IsDevelopment = 0 or IsDevelopment = @isDevelopment
	order by RT.[Level], m.[Order], RT.[Id];

	-- system parameters
	select [SysParams!TParam!Object]= null, [AppTitle], [AppSubTitle]
	from (select [Name], [Value]=StringValue from a2sys.SysParams) as s
		pivot (min([Value]) for [Name] in ([AppTitle], [AppSubTitle])) as p;
end
go
------------------------------------------------
create or alter procedure a2ui.[RegisterModule]
@ModuleId uniqueidentifier,
@Name nvarchar(255)
as
begin
	if not exists(select * from a2ui.Modules where [Id] = @ModuleId)
		insert into a2ui.Modules ([Id], [Name]) values (@ModuleId, @Name);
	else
		update a2ui.Modules set [Name] = @Name where [Id] = @ModuleId;
end
go
------------------------------------------------
create or alter procedure a2ui.[Tenant.ConnectModule]
@ModuleId uniqueidentifier,
@TenantId int
as
begin
	if not exists(select * from a2ui.TenantModules where Tenant = @TenantId and Module = @ModuleId)
		insert into a2ui.TenantModules(Tenant, Module) values (@TenantId, @ModuleId);
end
go
------------------------------------------------
create or alter procedure a2ui.[RegisterInitProcedure]
@Module uniqueidentifier,
@Procedure sysname
as
begin 
	set nocount on;
	set transaction isolation level read committed;

	if not exists(select * from a2ui.ModuleInitProcedures where [Procedure] = @Procedure and Module = @Module)
		insert into a2ui.ModuleInitProcedures(Module, [Procedure]) values (@Module, @Procedure);
end
go
------------------------------------------------
create or alter procedure a2ui.[RegisterTenantInitProcedure]
@Module uniqueidentifier,
@Procedure sysname
as
begin 
	set nocount on;
	set transaction isolation level read committed;

	if not exists(select * from a2ui.TenantInitProcedures where [Procedure] = @Procedure and Module = @Module)
		insert into a2ui.TenantInitProcedures(Module, [Procedure]) values (@Module, @Procedure);
end
go
------------------------------------------------
create or alter procedure a2ui.[InvokeInitProcedures]
@TenantId int
as
begin 
	set nocount on;
	set transaction isolation level read committed;

	declare @procName sysname;
	declare @moduleId uniqueidentifier;
	declare @prms nvarchar(255);
	declare @sql nvarchar(255);
	set @prms = N'@TenantId int, @ModuleId uniqueidentifier';
	declare #crs cursor local fast_forward read_only for
		select [Procedure], tm.Module from a2ui.TenantModules tm
		inner join a2ui.ModuleInitProcedures mip on tm.Module = mip.Module
		where tm.Tenant = @TenantId
		group by [Procedure], tm.[Module];
	open #crs;
	fetch next from #crs into @procName, @moduleId;
	while @@fetch_status = 0
	begin
		set @sql = N'exec ' + @procName + N' @TenantId = @TenantId, @ModuleId = @ModuleId';
		exec sp_executesql @sql, @prms, @TenantId, @moduleId;
		fetch next from #crs into @procName,@moduleId;
	end
	close #crs;
	deallocate #crs;
end
go
------------------------------------------------
create or alter procedure a2ui.[InvokeTenantInitProcedures]
@Tenants a2sys.[Id.TableType] readonly
as
begin 
	set nocount on;
	set transaction isolation level read committed;

	declare @procName sysname;
	declare @moduleId uniqueidentifier;
	declare @prms nvarchar(255);
	declare @sql nvarchar(255);
	set @prms = N'@Tenants a2sys.[Id.TableType] readonly, @ModuleId uniqueidentifier';
	declare #crs cursor local fast_forward read_only for
		select [Procedure], Module = m.Id
		from a2ui.Modules m
			inner join a2ui.TenantInitProcedures mip on m.Id = mip.Module
		group by [Procedure], m.[Id];
	open #crs;
	fetch next from #crs into @procName, @moduleId;
	while @@fetch_status = 0
	begin
		set @sql = N'exec ' + @procName + N' @Tenants = @Tenants, @ModuleId = @ModuleId';
		exec sp_executesql @sql, @prms, @Tenants, @moduleId;
		fetch next from #crs into @procName,@moduleId;
	end
	close #crs;
	deallocate #crs;
end
go
------------------------------------------------
create or alter procedure a2ui.[InvokeTenantInitProcedures.All]
as
begin 
	set nocount on;
	set transaction isolation level read committed;
	declare @Tenants a2sys.[Id.TableType];
	insert into @Tenants(Id)
		select Id from a2security.Tenants where Id <> 0;
	exec a2ui.[InvokeTenantInitProcedures] @Tenants;
end
go

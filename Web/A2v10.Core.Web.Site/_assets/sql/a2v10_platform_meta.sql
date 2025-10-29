/* _sqlscripts/a2v10_platform_meta.sql */

/*
Copyright © 2008-2025 Oleksandr Kukhtin

Last updated : 31 may 2025
module version : 8553
*/
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2sys')
	exec sp_executesql N'create schema a2sys authorization dbo';
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2security')
	exec sp_executesql N'create schema a2security authorization dbo';
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2ui')
	exec sp_executesql N'create schema a2ui authorization dbo';
go

------------------------------------------------
alter authorization on schema::a2sys to dbo;
alter authorization on schema::a2security to dbo;
alter authorization on schema::a2ui to dbo;
go

------------------------------------------------
grant execute on schema ::a2sys to public;
grant execute on schema ::a2security to public;
grant execute on schema ::a2ui to public;
go

------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2sys' and TABLE_NAME=N'SysParams')
create table a2sys.SysParams
(
	[Name] sysname not null constraint PK_SysParams primary key,
	StringValue nvarchar(255) null,
	IntValue int null,
	DateValue datetime null,
	GuidValue uniqueidentifier null
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2security' and SEQUENCE_NAME=N'SQ_Users')
	create sequence a2security.SQ_Users as bigint start with 100 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2security' and TABLE_NAME=N'Users')
create table a2security.Users
(
	Id	bigint not null
		constraint PK_Users primary key
		constraint DF_Users_PK default(next value for a2security.SQ_Users),
	UserName nvarchar(255) not null constraint UNQ_Users_UserName unique,
	DomainUser nvarchar(255) null,
	Void bit not null constraint DF_Users_Void default(0),
	SecurityStamp nvarchar(max) not null,
	PasswordHash nvarchar(max) null,
	/*for .net core compatibility*/
	SecurityStamp2 nvarchar(max) null,
	PasswordHash2 nvarchar(max) null,
	TwoFactorEnabled bit not null constraint DF_Users_TwoFactorEnabled default(0),
	AuthenticatorKey nvarchar(64) null,
	Email nvarchar(255) null,
	EmailConfirmed bit not null constraint DF_Users_EmailConfirmed default(0),
	PhoneNumber nvarchar(255) null,
	PhoneNumberConfirmed bit not null constraint DF_Users_PhoneNumberConfirmed default(0),
	LockoutEnabled	bit	not null constraint DF_Users_LockoutEnabled default(1),
	LockoutEndDateUtc datetimeoffset null,
	AccessFailedCount int not null constraint DF_Users_AccessFailedCount default(0),
	[Locale] nvarchar(32) not null constraint DF_Users_Locale2 default(N'uk-UA'),
	PersonName nvarchar(255) null,
	LastLoginDate datetime null, /*UTC*/
	LastLoginHost nvarchar(255) null,
	Memo nvarchar(255) null,
	ChangePasswordEnabled bit not null constraint DF_Users_ChangePasswordEnabled default(1),
	RegisterHost nvarchar(255) null,
	SetPassword bit,
	IsApiUser bit constraint DF_UsersIsApiUser default(0),
	IsExternalLogin bit constraint DF_UsersIsExternalLogin default(0),
	IsBlocked bit constraint DF_UsersIsBlocked default(0),
	UtcDateCreated datetime not null constraint DF_Users_UtcDateCreated default(getutcdate())
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2security' and TABLE_NAME = N'Users' and COLUMN_NAME = N'IsBlocked')
	alter table a2security.Users add IsBlocked bit constraint DF_UsersIsBlocked default(0);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2security' and TABLE_NAME = N'Users' and COLUMN_NAME = N'AuthenticatorKey')
	alter table a2security.Users add AuthenticatorKey nvarchar(64) null;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2security' and TABLE_NAME=N'ApiUserLogins')
create table a2security.ApiUserLogins
(
	[User] bigint not null
		constraint FK_ApiUserLogins_User_Users foreign key references a2security.Users(Id),
	[Mode] nvarchar(16) not null, -- ApiKey, OAuth2, JWT
	[ClientId] nvarchar(255),
	[ClientSecret] nvarchar(255),
	[ApiKey] nvarchar(1023),
	[AllowIP] nvarchar(1024),
	Memo nvarchar(255),
	RedirectUrl nvarchar(255),
	[UtcDateModified] datetime not null constraint DF_ApiUserLogins_DateModified default(getutcdate()),
	constraint PK_ApiUserLogins primary key clustered ([User], Mode) with (fillfactor = 70),

);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2security' and TABLE_NAME=N'ExternalUserLogins')
create table a2security.ExternalUserLogins
(
	[User] bigint not null
		constraint FK_ExternalUserLogins_User_Users foreign key references a2security.Users(Id),
	[LoginProvider] nvarchar(255) not null,
	[ProviderKey] nvarchar(max) not null,
	[UtcDateModified] datetime not null constraint DF_ExternalUserLogins_DateModified default(getutcdate()),
	constraint PK_ExternalUserLogins primary key clustered ([User], LoginProvider) with (fillfactor = 70),
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2security' and TABLE_NAME=N'DeletedUsers')
create table a2security.DeletedUsers
(
	Id	bigint not null
		constraint PK_DeletedUsers primary key
		constraint FK_DeletedUsers_DeletedByUser_Users foreign key references a2security.Users(Id),
	UserName nvarchar(255) not null,
	DomainUser nvarchar(255) null,
	Email nvarchar(255) null,
	PhoneNumber nvarchar(255) null,
	DeletedByUser bigint not null,
	UtcDateDeleted datetime not null constraint DF_Users_UtcDateDeleted default(getutcdate())
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2security' and TABLE_NAME=N'RefreshTokens')
create table a2security.RefreshTokens
(
	UserId bigint not null
		constraint FK_RefreshTokens_UserId_Users foreign key references a2security.Users(Id),
	[Provider] nvarchar(64) not null,
	[Token] nvarchar(255) not null,
	Expires datetime not null,
	constraint PK_RefreshTokens primary key (UserId, [Provider], Token) with (fillfactor = 70)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where [TABLE_SCHEMA] = N'a2security' and TABLE_NAME = N'KeyVaults')
create table a2security.[KeyVaults]
(
	[Key] nvarchar(255) not null
		constraint PK_KeyVaults primary key,	
	[Value] nvarchar(max),
	[Expired] datetime null
)
go
------------------------------------------------
create or alter view a2security.ViewUsers
as
	select Id, UserName, DomainUser, PasswordHash, SecurityStamp, Email, PhoneNumber,
		LockoutEnabled, AccessFailedCount, LockoutEndDateUtc, TwoFactorEnabled, [Locale],
		PersonName, Memo, Void, LastLoginDate, LastLoginHost, EmailConfirmed,
		PhoneNumberConfirmed, RegisterHost, ChangePasswordEnabled,
		SecurityStamp2, PasswordHash2, SetPassword, IsBlocked, AuthenticatorKey
	from a2security.Users u
	where Void = 0 and Id <> 0;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2ui' and TABLE_NAME=N'Menu')
create table a2ui.Menu
(
	Id uniqueidentifier not null
		constraint PK_Menu primary key,
	Parent uniqueidentifier
		constraint FK_Menu_Parent_Menu foreign key references a2ui.Menu(Id),
	[Name] nvarchar(255),
	[Url] nvarchar(255),
	CreateName nvarchar(255),
	CreateUrl nvarchar(255),
	Icon nvarchar(255),
	[Order] int not null constraint DF_Menu_Order default(0),
	[ClassName] nvarchar(255) null,
	[IsDevelopment] bit constraint DF_Menu_IsDevelopment default(0)
);
go

/*
Copyright © 2008-2025 Oleksandr Kukhtin

Last updated : 31 may 2025
module version : 8553
*/
------------------------------------------------
create or alter procedure a2sys.[AppTitle.Load]
as
begin
	set nocount on;
	select [AppTitle], [AppSubTitle]
	from (select [Name], [Value] = StringValue from a2sys.SysParams) as s
		pivot (min(Value) for [Name] in ([AppTitle], [AppSubTitle])) as p;
end
go

------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.DOMAINS where DOMAIN_SCHEMA=N'a2sys' and DOMAIN_NAME=N'Id.TableType' and DATA_TYPE=N'table type')
create type a2sys.[Id.TableType] as table(
	Id bigint null
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.DOMAINS where DOMAIN_SCHEMA=N'a2sys' and DOMAIN_NAME=N'GUID.TableType' and DATA_TYPE=N'table type')
create type a2sys.[GUID.TableType] as table(
	Id uniqueidentifier null
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.DOMAINS where DOMAIN_SCHEMA=N'a2sys' and DOMAIN_NAME=N'NameValue.TableType' and DATA_TYPE=N'table type')
create type a2sys.[NameValue.TableType] as table(
	[Name] nvarchar(255),
	[Value] nvarchar(max)
);
go


/*
Copyright © 2008-2025 Oleksandr Kukhtin

Last updated : 04 jun 2025
module version : 8553
*/

-- SECURITY
------------------------------------------------
create or alter procedure a2security.FindUserById
@Id bigint
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select * from a2security.ViewUsers where Id=@Id;
end
go
------------------------------------------------
create or alter procedure a2security.FindUserByEmail
@Email nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select * from a2security.ViewUsers where Email=@Email;
end
go
------------------------------------------------
create or alter procedure a2security.FindUserByName
@UserName nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select * from a2security.ViewUsers where UserName=@UserName;
end
go
------------------------------------------------
create or alter procedure a2security.FindUserByPhoneNumber
@PhoneNumber nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select * from a2security.ViewUsers where PhoneNumber=@PhoneNumber;
end
go
------------------------------------------------
create or alter procedure a2security.UpdateUserLogin
@Id bigint,
@LastLoginHost nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set LastLoginDate = getutcdate(), LastLoginHost = @LastLoginHost 
	where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetSetPassword]
@Id bigint,
@Set bit = 0
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set SetPassword = @Set where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetAccessFailedCount]
@Id bigint,
@AccessFailedCount int
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set AccessFailedCount = @AccessFailedCount
	where Id=@Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetEMailConfirmed]
@Id bigint,
@Confirmed bit
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set EmailConfirmed = @Confirmed
	where Id=@Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetLockoutEndDate]
@Id bigint,
@LockoutEndDate datetimeoffset
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set LockoutEndDateUtc = @LockoutEndDate where Id=@Id;
end
go

------------------------------------------------
create or alter procedure a2security.[User.SetPasswordHash]
@Id bigint,
@PasswordHash nvarchar(max)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set PasswordHash2 = @PasswordHash where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetSecurityStamp]
@Id bigint,
@SecurityStamp nvarchar(max)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set SecurityStamp2 = @SecurityStamp where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetPhoneNumberConfirmed]
@Id bigint,
@PhoneNumber nvarchar(255),
@Confirmed bit
as
begin
	set nocount on;
	set transaction isolation level read committed;
	update a2security.ViewUsers set PhoneNumber = @PhoneNumber, PhoneNumberConfirmed = @Confirmed where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.FindApiUserByApiKey
@Host nvarchar(255) = null,
@ApiKey nvarchar(1023) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @status nvarchar(255);
	declare @code int;

	set @status = N'ApiKey=' + @ApiKey;
	set @code = 65; /*fail*/

	declare @user table(Id bigint, [Name] nvarchar(255), ClientId nvarchar(255), AllowIP nvarchar(255), Locale nvarchar(32));
	insert into @user(Id, [Name], ClientId, AllowIP, Locale)
	select top(1) u.Id, [Name]=u.UserName, s.ClientId, s.AllowIP, u.Locale 
	from a2security.Users u inner join a2security.ApiUserLogins s on u.Id = s.[User]
	where u.Void=0 and s.Mode = N'ApiKey' and s.ApiKey=@ApiKey;
	
	if @@rowcount > 0 
	begin
		set @code = 64 /*sucess*/;
		update a2security.Users set LastLoginDate=getutcdate(), LastLoginHost=@Host
		  from @user t inner join a2security.Users u on t.Id = u.Id;
	end

	--insert into a2security.[Log] (UserId, Severity, Code, Host, [Message])
		--values (0, N'I', @code, @Host, @status);

	select * from @user;
end
go
------------------------------------------------
create or alter procedure a2security.FindUserByExternalLogin
@LoginProvider nvarchar(255),
@ProviderKey nvarchar(1024)
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @userId bigint;
	select @userId = [User] from a2security.ExternalUserLogins e
	where LoginProvider=@LoginProvider and ProviderKey = @ProviderKey;

	update a2security.Users set LastLoginDate = getutcdate() where Id = @userId;

	select u.* from a2security.ViewUsers u
	where Id = @userId
end
go
------------------------------------------------
create or alter procedure a2security.[User.AddExternalLogin]
@Id bigint,
@LoginProvider nvarchar(255),
@ProviderKey nvarchar(1024)
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	begin tran;
	delete from a2security.ExternalUserLogins where [User] = @Id and LoginProvider = @LoginProvider;

	insert into a2security.ExternalUserLogins([User], LoginProvider, ProviderKey)
	values (@Id, @LoginProvider, @ProviderKey);
	commit tran;
end
go
------------------------------------------------
create or alter procedure a2security.CreateUser 
@UserName nvarchar(255),
@PasswordHash nvarchar(max) = null,
@SecurityStamp nvarchar(max),
@Email nvarchar(255) = null,
@PhoneNumber nvarchar(255) = null,
@PersonName nvarchar(255) = null,
@Memo nvarchar(255) = null,
@Locale nvarchar(255) = null,
@RetId bigint output
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	set @Locale = isnull(@Locale, N'uk-UA');

	declare @tenants table(Id int);
	declare @users table(Id bigint);
	declare @userId bigint;
	declare @tenantCreated bit = 0;

	begin tran;

	insert into a2security.Users(UserName, PersonName, Email, PhoneNumber, SecurityStamp, PasswordHash,
		Locale, Memo)
	output inserted.Id into @users(Id)
	values (@UserName, @PersonName, @Email, @PhoneNumber, @SecurityStamp, @PasswordHash, 
		@Locale, @Memo);
	select top(1) @userId = Id from @users;
	set @RetId = @userId;
	commit tran;
end
go
------------------------------------------------
create or alter procedure a2security.[User.RegisterComplete]
@Id bigint
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	update a2security.Users set EmailConfirmed = 1, LastLoginDate = getutcdate()
	where Id = @Id;

	select * from a2security.ViewUsers where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.GetUserGroups
@UserId bigint
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
end
go
------------------------------------------------
create or alter procedure a2security.[User.UpdateParts]
@Id bigint,
@PhoneNumber nvarchar(255) = null,
@PersonName nvarchar(255) = null,
@EmailConfirmed bit = null,
@FirstName nvarchar(255) = null,
@LastName nvarchar(255) = null,
@Locale nvarchar(32) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.Users set 
		PhoneNumber = isnull(@PhoneNumber, PhoneNumber),
		PersonName = isnull(@PersonName, PersonName),
		EmailConfirmed = isnull(@EmailConfirmed, EmailConfirmed),
		Locale = isnull(@Locale, Locale)
	where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetAuthenticatorKey]
@Id bigint,
@AuthenticatorKey nvarchar(64)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.Users set AuthenticatorKey = @AuthenticatorKey  where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetTwoFactorEnabled]
@Id bigint,
@TwoFactorEnabled bit
as
begin
	set nocount on;
	set transaction isolation level read committed;

	if @TwoFactorEnabled = 1
		update a2security.Users set TwoFactorEnabled = 1 where Id = @Id;
	else
		update a2security.Users set TwoFactorEnabled = 0, AuthenticatorKey = null where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.CreateApiUser]
@UserId bigint,
@ApiKey nvarchar(1023),
@Name nvarchar(255) = null,
@PersonName nvarchar(255) = null,
@Memo nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	declare @users table(Id bigint);
	declare @newid bigint;

	begin tran;
		insert into a2security.Users(IsApiUser, UserName, PersonName, Memo, Locale, SecurityStamp, SecurityStamp2)
		output inserted.Id into @users(Id)
		select 1, @Name, @PersonName, @Memo, u.Locale, N'', N''
		from a2security.Users u where Id = @UserId;

		select top(1) @newid = Id from @users;

		insert into a2security.ApiUserLogins([User], Mode, ApiKey) values
			(@newid, N'ApiKey', @ApiKey);
	commit tran;

	select Id, UserName, PersonName, Memo, Locale from a2security.ViewUsers where Id = @newid;
end
go
------------------------------------------------
create or alter procedure a2security.[User.DeleteApiUser]
@UserId bigint,
@Id bigint
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	-- TODO: isTenantAdmin

	begin tran
	update a2security.Users set Void = 1
		where Id = @Id;
	delete from a2security.ApiUserLogins where [User] = @Id;
	commit tran;

	-- We can't use ViewUsers (Void = 0)
	select Id from a2security.Users where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.DeleteUser]
@UserId bigint = null,
@Id bigint
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	declare @guid nvarchar(50);
	set @guid = cast(newid() as nvarchar(50));
	begin tran;

	insert into a2security.DeletedUsers(Id, UserName, DomainUser, Email, PhoneNumber, DeletedByUser)
	select Id, UserName, DomainUser, Email, PhoneNumber, @UserId
	from a2security.Users where Id = @Id;

	update a2security.Users set Void = 1, SecurityStamp = N'', SecurityStamp2 = N'', PasswordHash = null, PasswordHash2 = null,
		EmailConfirmed = 0, PhoneNumberConfirmed = 0,
		UserName = @guid, Email = @guid, PhoneNumber = @guid, DomainUser = @guid
	where Id = @Id;

	-- We can't use ViewUsers (Void = 0)
	select Id, UserName, Email, PhoneNumber, DomainUser from a2security.Users where Id = @Id;

	commit tran;

end
go
------------------------------------------------
create or alter procedure a2security.[User.EditUser]
@UserId bigint,
@Id bigint,
@PersonName nvarchar(255) = null,
@PhoneNumber nvarchar(255) = null,
@Memo nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.Users set PersonName = @PersonName, Memo = @Memo, PhoneNumber = @PhoneNumber
		where Id = @Id;

	select Id, PersonName, PhoneNumber, Memo from a2security.ViewUsers where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[AddToken]
@Id bigint,
@Provider nvarchar(64),
@Token nvarchar(255),
@Expires datetime,
@Remove nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;
	begin tran;
	insert into a2security.RefreshTokens(UserId, [Provider], Token, Expires)
		values (@Id, @Provider, @Token, @Expires);
	if @Remove is not null
		delete from a2security.RefreshTokens 
		where UserId = @Id and [Provider] = @Provider and Token = @Remove;
	commit tran;
end
go
------------------------------------------------
create or alter procedure a2security.[GetToken]
@Id bigint,
@Provider nvarchar(255),
@Token nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	select [Token], UserId, Expires from a2security.RefreshTokens
	where UserId = @Id and [Provider] = @Provider and Token = @Token;
end
go

------------------------------------------------
create or alter procedure a2security.[RemoveToken]
@Id bigint,
@Provider nvarchar(255),
@Token nvarchar(511)
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	delete from a2security.RefreshTokens 
	where UserId = @Id and [Provider] = @Provider and Token = @Token;
end
go

------------------------------------------------
create or alter procedure a2security.[KeyVault.Load]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Value] from a2security.KeyVaults;
end
go
------------------------------------------------
create or alter procedure a2security.[KeyVault.Update]
@Key nvarchar(32),
@Value nvarchar(max),
@Expired datetime = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	with TV as (
		select [Key] = @Key, [Value] = @Value, Expired = @Expired
	)
	merge a2security.KeyVaults as t
	using TV as s
	on t.[Key] = s.[Key]
	when matched then update set
		t.[Value] = s.[Value],
		t.Expired = s.[Expired]
	when not matched then insert
		([Key], [Value], [Expired]) values
		([Key], [Value], [Expired]);
end
go

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

/*
Copyright © 2008-2025 Oleksandr Kukhtin

Last updated : 31 may 2025
module version : 8553
*/
------------------------------------------------
if not exists(select * from a2security.Users)
begin
	set nocount on;
	set transaction isolation level read committed;

	insert into a2security.Users(Id, UserName, Email, SecurityStamp, PasswordHash, PersonName, EmailConfirmed)
	values (99, N'admin@admin.com', N'admin@admin.com', N'c9bb451a-9d2b-4b26-9499-2d7d408ce54e', N'AJcfzvC7DCiRrfPmbVoigR7J8fHoK/xdtcWwahHDYJfKSKSWwX5pu9ChtxmE7Rs4Vg==',
		N'System administrator', 1);
end
go


/*
Copyright � 2025 Oleksandr Kukhtin

Last updated : 09 jun 2025
module version : 8554
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
		constraint DF_DefaultColumns_Required default(0),
	[Total] bit not null
		constraint DF_DefaultColumns_Total default(0)
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
	[Required] bit,
	[Total] bit,
	[Unique] bit
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
		c.[MaxLength], c.[Role], c.Computed, c.[Required], c.[Total], c.[Unique], c.[Order], DbOrder = tvc.column_id,
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
		--and r.[Name] = @Table collate SQL_Latin1_General_CP1_CI_AI
		and r.Kind in (N'folder') and IsFolder = 1;

	select [Table!TTable!Object] = null, [!!Id] = c.Id, c.[Schema], 
		[Name] = N'Operations' /*!!!*/, c.ItemName, c.ItemsName,
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
create or alter procedure a2meta.[Table.Form.Reset] 
@Id uniqueidentifier,
@Key nvarchar(32)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	delete from a2meta.Forms where [Table] = @Id and [Key] = @Key;

	select [Table!TTable!Object] = null, [Id!!Id] = Id, [Schema], [Name]
	from a2meta.[Catalog] where Id = @Id;
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
		[MaxLength] int, Ref nvarchar(32), [Role] int, [Order] int, [Required] bit, [Total] bit);

	insert into @defCols([Order], [Schema], Kind, [Name], [Role], [Required], [Total], DataType, [MaxLength], Ref) values
	-- Catalog
	(1, N'cat', N'table', N'Id',         1, 0, 0, N'id', null, null),
	(2, N'cat', N'table', N'Void',      16, 0, 0, N'bit', null, null),
	(3, N'cat', N'table', N'IsSystem', 128, 0, 0, N'bit', null, null),
	(4, N'cat', N'table', N'Name',       2, 1, 0, N'string',    100, null),
	(5, N'cat', N'table', N'Memo',       0, 0, 0, N'string',    255, null),

	-- Document
	(1, N'doc', N'table', N'Id',         1, 0, 0, N'id',        null, null),
	(2, N'doc', N'table', N'Void',      16, 0, 0, N'bit',       null, null),
	(3, N'doc', N'table', N'Done',     256, 0, 0, N'bit',       null, null),
	(4, N'doc', N'table', N'Date',       0, 0, 0, N'date',      null, null),
	(5, N'doc', N'table', N'Number',  2048, 0, 0, N'string',      32, null),
	(6, N'doc', N'table', N'Operation',  0, 0, 0, N'operation', null, null),
	(7, N'doc', N'table', N'Name',       2, 0, 0, N'string',     100, null), -- todo: computed
	(8, N'doc', N'table', N'Sum',        0, 0, 0, N'money',     null, null),
	(9, N'doc', N'table', N'Memo',       0, 0, 0, N'string',     255, null),
	
	-- cat.Details
	(1, N'cat', N'details', N'Id',      1, 0, 0, N'id',  null, null),
	(2, N'cat', N'details', N'Parent', 32, 0, 0, N'reference', null, N'parent'),
	(3, N'cat', N'details', N'RowNo',   8, 0, 0, N'int', null, null),

	-- doc.Details
	(1, N'doc', N'details', N'Id',       1, 0, 0, N'id',   null, null),
	(2, N'doc', N'details', N'Parent',  32, 0, 0, N'reference',  null, N'parent'),
	(3, N'doc', N'details', N'RowNo',    8, 0, 0, N'int',  null, null),
	(4, N'doc', N'details', N'Kind',   512, 0, 0, N'string', 32, null),
	(5, N'doc', N'details', N'Qty',      0, 1, 0, N'float',null, null),
	(6, N'doc', N'details', N'Price',    0, 1, 0, N'float',null, null),
	(7, N'doc', N'details', N'Sum',      0, 0, 1, N'money',null, null),
	
	-- jrn.Journal
	(1, N'jrn', N'table', N'Id',       1, 0, 0, N'id',       null, null),
	(2, N'jrn', N'table', N'Date',     0, 0, 0, N'datetime', null, null),
	(3, N'jrn', N'table', N'InOut',    0, 0, 0, N'int',      null, null),
	(4, N'jrn', N'table', N'Qty',      0, 0, 0, N'float',    null, null),
	(5, N'jrn', N'table', N'Sum',      0, 0, 0, N'money',    null, null),

	-- op.Operations
	(1, N'op', N'optable', N'Id',       1, 0, 0, N'id',      null, null),
	(2, N'op', N'optable', N'Name',     2, 0, 0, N'string',    64, null),
	(3, N'op', N'optable', N'Url',      0, 0, 0, N'string',   255, null),
	(4, N'op', N'optable', N'Category', 0, 0, 0, N'string',    32, null),
	(5, N'op', N'optable', N'Void',    16, 0, 0, N'bit',     null, null);

	insert into a2meta.DefaultColumns ([Schema], Kind, [Name], DataType, [MaxLength], Ref, [Role], [Order], [Required], [Total])
	select [Schema], Kind, [Name], DataType, [MaxLength], Ref, [Role], [Order], [Required], [Total]
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
create or alter procedure a2meta.[Config.Export]
@UserId bigint
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	-- FOR DOWNLOAD/UPLOAD
	select [Catalog!TCatalog!Array] = null, [Id!!Id] = Id, FParent = Parent, ParentTable, IsFolder, 
		[Order], [Schema], [Name], Kind, ItemsName, ItemName, TypeName, EditWith, Source,
		ItemsLabel, ItemLabel, UseFolders, FolderMode, [Type]
	from a2meta.[Catalog];

	select [Application!TApplication!Array] = null, [Id!!Id] = Id, [Name], [Title], IdDataType, 
		Memo, [Version]
	from a2meta.[Application];

	select [Columns!TColumn!Array] = null, [Id!!Id] = Id, [Table], [Name], [Label], DataType,
		[MaxLength], Reference, [Order], [Role], Source, Computed, [Required], Total, [Unique]
	from a2meta.[Columns];

	select [DetailsKinds!TDetailKind!Array] = null, [Id!!Id] = Id, Details, [Order],
		[Name], [Label]
	from a2meta.DetailsKinds;

	select [Apply!TApply!Array] = null, [Id!!Id] = Id, [Table], Journal, [Order], Details, InOut,
		Storno, Kind
	from a2meta.Apply;

	select [ApplyMapping!TApplyMap!Array] = null, [Id!!Id] = Id, Apply, [Target], Source
	from a2meta.ApplyMapping;

	select [EnumItems!TEnumItem!Array] = null, [Id!!Id] = Id, Enum, [Name], [Label],
		[Order], Inactive
	from a2meta.EnumItems;

	select [Forms!TForm!Array] = null, [Table], [Key], [Json]
	from a2meta.Forms;

	select [MenuItems!TMenuItem!Array] = null, [Id!!Id] = Id, Interface, FParent = Parent, [Name], 
		[Url], CreateName, CreateUrl, [Order], Source
	from a2meta.MenuItems;

	select [ReportItems!TRepItem!Array] = null, [Id!!Id] = Id, Report, [Column], Kind, [Order], [Label], 
		Func, Checked
	from a2meta.ReportItems;
end
go
------------------------------------------------
drop procedure if exists a2meta.[Config.Export.Metadata];
drop procedure if exists a2meta.[Config.Export.Update];
drop type if exists a2meta.[Config.Export.Catalog.TableType];
drop type if exists a2meta.[Config.Export.Application.TableType];
drop type if exists a2meta.[Config.Export.DetailsKind.TableType];
drop type if exists a2meta.[Config.Export.Column.TableType];
drop type if exists a2meta.[Config.Export.Apply.TableType];
drop type if exists a2meta.[Config.Export.ApplyMap.TableType];
drop type if exists a2meta.[Config.Export.EnumItem.TableType];
drop type if exists a2meta.[Config.Export.Form.TableType];
drop type if exists a2meta.[Config.Export.MenuItem.TableType];
drop type if exists a2meta.[Config.Export.RepItem.TableType];
go
------------------------------------------------
create type a2meta.[Config.Export.Catalog.TableType] as table 
(
	[Id] uniqueidentifier,
	[FParent] uniqueidentifier,
	[ParentTable] uniqueidentifier,
	IsFolder bit,
	[Order] int,
	[Schema] nvarchar(32), 
	[Name] nvarchar(128),
	[Kind] nvarchar(32),
	ItemsName nvarchar(128),
	ItemName nvarchar(128),
	TypeName nvarchar(128),
	EditWith nvarchar(16),
	Source nvarchar(255),
	ItemsLabel nvarchar(255),
	ItemLabel nvarchar(128),
	UseFolders bit,
	FolderMode nvarchar(16),
	[Type] nvarchar(32)
);
go
------------------------------------------------
create type a2meta.[Config.Export.Column.TableType] as table 
(
	[Id] uniqueidentifier,
	[Table] uniqueidentifier,
	[Name] nvarchar(128),
	[Label] nvarchar(255),
	[DataType] nvarchar(32),
	[MaxLength] int,
	Reference uniqueidentifier,
	[Order] int,
	[Role] int,
	Source nvarchar(255),
	Computed nvarchar(255),
	[Required] bit,
	[Total] bit,
	[Unique] bit
)
go
------------------------------------------------
create type a2meta.[Config.Export.Application.TableType] as table 
(
	[Id] uniqueidentifier,
	[Name] nvarchar(255),
	[Title] nvarchar(255),
	IdDataType nvarchar(32),
	Memo nvarchar(255),
	[Version] int not null
);
go
------------------------------------------------
create type a2meta.[Config.Export.DetailsKind.TableType] as table
(
	[Id] uniqueidentifier,
	[Details] uniqueidentifier,
	[Order] int,
	[Name] nvarchar(32),
	[Label] nvarchar(255)
);
go
------------------------------------------------
create type a2meta.[Config.Export.Apply.TableType] as table
(
	[Id] uniqueidentifier,
	[Table] uniqueidentifier,
	[Journal] uniqueidentifier,
	[Order] int,
	Details uniqueidentifier,
	[InOut] smallint,
	Storno bit,
	Kind uniqueidentifier
);
go
------------------------------------------------
create type a2meta.[Config.Export.ApplyMap.TableType] as table 
(
	[Id] uniqueidentifier,
	[Apply] uniqueidentifier,
	[Target] uniqueidentifier,
	[Source] uniqueidentifier
);
go
------------------------------------------------
create type a2meta.[Config.Export.EnumItem.TableType] as table
(
	[Id] uniqueidentifier,
	[Enum] uniqueidentifier,
	[Name] nvarchar(16),
	[Label] nvarchar(255),
	[Order] int,
	[Inactive] bit
);
go
------------------------------------------------
create type a2meta.[Config.Export.Form.TableType] as table
(
	[Table] uniqueidentifier,
	[Key] nvarchar(64),
	[Json] nvarchar(max)
);
go
------------------------------------------------
create type a2meta.[Config.Export.MenuItem.TableType] as table
(
	[Id] uniqueidentifier,
	[Interface] uniqueidentifier,
	[FParent] uniqueidentifier,
	[Name] nvarchar(255),
	[Url] nvarchar(255),
	[CreateName] nvarchar(255),
	[CreateUrl] nvarchar(255),
	[Order] int,
	Source nvarchar(255)
);
go
------------------------------------------------
create type a2meta.[Config.Export.RepItem.TableType] as table
(
	[Id] uniqueidentifier,
	[Report] uniqueidentifier,
	[Column] uniqueidentifier,
	[Kind] nchar(1),
	[Order] int,
	[Label] nvarchar(255),
	Func nvarchar(32), 
	[Checked] bit
);
go
------------------------------------------------
create or alter procedure a2meta.[Config.Export.Metadata]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @Catalog a2meta.[Config.Export.Catalog.TableType];
	declare @Columns a2meta.[Config.Export.Column.TableType];
	declare @Application a2meta.[Config.Export.Application.TableType];
	declare @DetailsKinds a2meta.[Config.Export.DetailsKind.TableType];
	declare @Apply a2meta.[Config.Export.Apply.TableType];
	declare @ApplyMapping a2meta.[Config.Export.ApplyMap.TableType];
	declare @EnumItems a2meta.[Config.Export.EnumItem.TableType];
	declare @Forms a2meta.[Config.Export.Form.TableType];
	declare @MenuItems a2meta.[Config.Export.MenuItem.TableType];
	declare @ReportItems a2meta.[Config.Export.RepItem.TableType];

	select [Catalog!Catalog!Metadata]= null, * from @Catalog;
	select [Application!Application!Metadata]= null, * from @Application;
	select [Columns!Columns!Metadata]= null, * from @Columns;
	select [DetailsKinds!DetailsKinds!Metadata] = null, * from @DetailsKinds;
	select [Apply!Apply!Metadata] = null, * from @Apply;
	select [ApplyMapping!ApplyMapping!Metadata] = null, * from @ApplyMapping;
	select [EnumItems!EnumItems!Metadata] = null, * from @EnumItems;
	select [Forms!Forms!Metadata] = null, * from @Forms;
	select [MenuItems!MenuItems!Metadata] = null, * from @MenuItems;
	select [ReportItems!ReportItems!Metadata] = null, * from @ReportItems;
end
go
------------------------------------------------
create or alter procedure a2meta.[Config.Export.Update]
@UserId bigint,
@Catalog a2meta.[Config.Export.Catalog.TableType] readonly,
@Application a2meta.[Config.Export.Application.TableType] readonly,
@Columns a2meta.[Config.Export.Column.TableType] readonly,
@DetailsKinds a2meta.[Config.Export.DetailsKind.TableType] readonly,
@Apply a2meta.[Config.Export.Apply.TableType] readonly,
@ApplyMapping a2meta.[Config.Export.ApplyMap.TableType] readonly,
@EnumItems a2meta.[Config.Export.EnumItem.TableType] readonly,
@Forms a2meta.[Config.Export.Form.TableType] readonly,
@MenuItems a2meta.[Config.Export.MenuItem.TableType] readonly,
@ReportItems a2meta.[Config.Export.RepItem.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	declare @currentRootId uniqueidentifier, @newRootId uniqueidentifier;
	select @currentRootId = Id from a2meta.[Catalog] where Kind = N'root';
	select @newRootId = Id from @Catalog where Kind = N'root';

	if @currentRootId <> @newRootId
	begin
		-- REPLACE OLD APPLICATION
		delete from a2meta.Columns;
		delete from a2meta.[Application];
		delete from a2meta.[Catalog];
	end

	merge a2meta.[Catalog] as t
	using @Catalog as s
	on t.Id = s.Id
	when matched then update set
		t.[Order] = s.[Order],
		t.[Schema] = s.[Schema],
		t.[Name] = s.[Name],
		t.[Kind] = s.[Kind],
		t.ItemsName = s.ItemsName,
		t.ItemName = s.ItemName,
		t.TypeName = s.TypeName,
		t.EditWith = s.EditWith,
		t.Source = s.Source,
		t.ItemsLabel = s.ItemsLabel,
		t.ItemLabel = s.ItemLabel,
		t.UseFolders = s.UseFolders,
		t.FolderMode = s.FolderMode,
		t.[Type] = s.[Type]
	when not matched then insert
	([Id], [Parent], [ParentTable], IsFolder, [Order], [Schema], [Name], [Kind], ItemsName, ItemName, TypeName,
		EditWith, Source, ItemsLabel, ItemLabel, UseFolders, FolderMode, [Type]) values
	([Id], [FParent], [ParentTable], isnull(IsFolder, 0), [Order], [Schema], [Name], [Kind], ItemsName, ItemName, TypeName,
		EditWith, Source, ItemsLabel, ItemLabel, UseFolders, FolderMode, [Type]);

	merge a2meta.[Application] as t
	using @Application as s
	on t.Id = s.Id
	when matched then update set
		t.[Name] = s.[Name],
		t.[Title] = s.[Title],
		t.IdDataType = s.IdDataType,
		t.Memo = s.Memo,
		t.[Version] = s.[Version]
	when not matched then insert
	([Id], [Name], [Title], IdDataType, Memo, [Version]) values
	([Id], [Name], [Title], IdDataType, Memo, [Version]);

	merge a2meta.Columns as t
	using @Columns as s
	on t.Id = s.Id
	when matched then update set
		t.[Name] = s.[Name],
		t.[Label] = s.[Label],
		t.[DataType] = s.[DataType],
		t.[MaxLength] = s.[MaxLength],
		t.Reference = s.Reference,
		t.[Order] = s.[Order],
		t.[Role] = isnull(s.[Role], 0),
		t.Source = s.Source,
		t.Computed = s.Computed,
		t.[Required] = s.[Required],
		t.[Total] = s.[Total],
		t.[Unique] = s.[Unique]
	when not matched then insert 
	([Id], [Table], [Name], [Label], [DataType], [MaxLength], Reference, [Order],
		[Role], Source, Computed, [Required], [Total], [Unique]) values
	([Id], [Table], [Name], [Label], [DataType], [MaxLength], Reference, [Order],
		isnull([Role], 0), Source, Computed, [Required], [Total], [Unique]);

	merge a2meta.DetailsKinds as t
	using @DetailsKinds as s
	on t.Id = s.Id
	when matched then update set
		t.[Details] = s.[Details],
		t.[Order] = s.[Order],
		t.[Name] = s.[Name],
		t.[Label] = s.[Label]
	when not matched then insert
		([Id], [Details], [Order], [Name], [Label]) values
		([Id], [Details], [Order], [Name], [Label]);

	merge a2meta.Apply as t
	using @Apply as s
	on t.Id = s.Id
	when matched then update set
		t.[Table] = s.[Table],
		t.[Journal] = s.[Journal],
		t.[Order] = s.[Order],
		t.[Details] = s.[Details],
		t.InOut = s.InOut,
		t.Storno = isnull(s.Storno, 0),
		t.Kind = s.Kind
	when not matched then insert
		([Id], [Table], Journal, [Order], [Details], InOut, Storno, Kind) values
		([Id], [Table], Journal, [Order], [Details], InOut, isnull(Storno, 0), Kind);

	merge a2meta.ApplyMapping as t
	using @ApplyMapping as s
	on t.Id = s.Id
	when matched then update set
		t.[Apply] = s.[Apply],
		t.[Target] = s.[Target],
		t.[Source] = s.[Source]
	when not matched then insert
		([Id], [Apply], [Target], [Source]) values
		([Id], [Apply], [Target], [Source]);

	merge a2meta.EnumItems as t
	using @EnumItems as s
	on t.Id = s.Id
	when matched then update set
		t.[Enum] = s.[Enum],
		t.[Name] = s.[Name],
		t.[Label] = s.[Label],
		t.[Order] = s.[Order],
		t.Inactive = s.Inactive
	when not matched then insert
		([Id], [Enum], [Name], [Label], [Order], Inactive) values
		([Id], [Enum], [Name], [Label], [Order], Inactive);

	merge a2meta.Forms as t
	using @Forms as s
	on t.[Table] = s.[Table] and t.[Key] = s.[Key]
	when matched then update set
		t.[Json] = s.[Json]
	when not matched then insert
		([Table], [Key], [Json]) values
		([Table], [Key], [Json]);

	merge a2meta.MenuItems as t
	using @MenuItems as s
	on t.Id = s.Id
	when matched then update set
		t.[Interface] = s.[Interface],
		t.[Parent] = s.[FParent],
		t.[Name] = s.[Name],
		t.[Url] = s.[Url],
		t.CreateName = s.CreateName,
		t.CreateUrl = s.CreateUrl,
		t.[Order] = s.[Order],
		t.[Source] = s.[Source]
	when not matched then insert
		([Id], [Interface], Parent, [Name], [Url], CreateName, CreateUrl, [Order], [Source]) values
		([Id], [Interface], FParent, [Name], [Url], CreateName, CreateUrl, [Order], [Source]);

	merge a2meta.ReportItems as t
	using @ReportItems as s
	on t.Id = s.Id
	when matched then update set
		t.[Report] = s.[Report],
		t.[Column] = s.[Column],
		t.[Kind] = s.[Kind],
		t.[Order] = s.[Order],
		t.[Label] = s.[Label],
		t.Func = s.Func,
		t.Checked = s.Checked
	when not matched then insert
		([Id], [Report], [Column], [Kind], [Order], [Label], [Func], [Checked]) values
		([Id], [Report], [Column], [Kind], [Order], [Label], [Func], [Checked]);
end
go
------------------------------------------------
exec a2meta.[Catalog.Init];
go


-- Application Designer
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'cat')
	exec sp_executesql N'create schema cat authorization dbo';
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'doc')
	exec sp_executesql N'create schema doc authorization dbo';
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'jrn')
	exec sp_executesql N'create schema jrn authorization dbo';
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'op')
	exec sp_executesql N'create schema op authorization dbo';
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'enm')
	exec sp_executesql N'create schema enm authorization dbo';
go
------------------------------------------------
begin
	set nocount on;

	if not exists (select * from a2ui.Menu)
		insert into a2ui.Menu(Id, Parent, [Name], Icon, [Order], [ClassName]) values
		(N'00000000-0000-0000-0000-000000000000', null, N'Main', null, 0, null),
		(N'02393194-2D0C-4651-B7D0-C64A9B6E0A69', N'00000000-0000-0000-0000-000000000000', null, null, 890, N'grow'),
		(N'9F3B38D6-2344-4BD7-BEFA-47819E0EC2FF', N'00000000-0000-0000-0000-000000000000', N'@[Settings]', N'gear-outline', 900, null);
end
go
------------------------------------------------
create or alter procedure a2meta.[Config.Index]
@UserId bigint
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @root uniqueidentifier;
	select @root = Id from a2meta.[Catalog] where [Kind] = N'root' and Id = [Parent];

	select [Elems!TElem!Tree] = null, [Id!!Id] = c.Id, [Name!!Name] = c.[Name], c.IsFolder, c.[Schema], 
		Kind, ParentTable,
		[Items!TElem!Items] = null,
		[HasChildren!!HasChildren] = case when exists(select * from a2meta.[Catalog] ch where ch.Parent = c.Id) then 1 else 0 end,
		[!TElem.Items!ParentId] = nullif(Parent, @root)
	from a2meta.[Catalog] c
	where IsFolder = 1 or Kind = N'app'
	order by [Order];
end
go
------------------------------------------------
create or alter procedure a2meta.[Config.Expand]
@UserId bigint,
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	with T as (
		select [Id], [Parent], 
			IsFolder, [Name], [Schema], ParentTable, Kind, [Level] = 0, [Order]
		from a2meta.[Catalog] where Parent = @Id
		union all 
		select c.[Id], c.[Parent], c.IsFolder, c.[Name], c.[Schema], c.ParentTable, c.Kind, [Level] = T.[Level] + 1, c.[Order] 
		from a2meta.[Catalog] c inner join T on c.Parent = T.Id
	)
	select [Items!TElem!Tree] = null, [Id!!Id] = Id, [Name!!Name] = [Name], IsFolder, [Schema], 
		Kind, ParentTable,
		[Items!TElem!Items] = null,
		[!TElem.Items!ParentId] = nullif(Parent, @Id)
	from T
	order by [Level], 
		case when [Schema] = 'ui' then cast([Order] as nvarchar(255)) else [Name] end;
end
go
------------------------------------------------
create or alter procedure a2meta.[Config.CreateItem]
@UserId bigint,
@Parent uniqueidentifier,
@Schema nvarchar(32),
@Name nvarchar(255),
@Kind nvarchar(32)
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	
	declare @Id uniqueidentifier = newid();
	declare @order int;
	select @order = isnull(max([Order]), 0) + 1 from a2meta.[Catalog] where Parent = @Parent;

	insert into a2meta.[Catalog] (Id, Parent, [Schema], [Name], Kind, EditWith, [Order])
	values (@Id, @Parent, @Schema, @Name, @Kind, 
		case @Schema 
			when  N'doc' then N'Page'
			when  N'op' then N'Page'
			else N'Dialog' 
		end,
		@order
	);

	insert into a2meta.Columns ([Table], [Name], [DataType], [MaxLength], Reference, [Role], [Order], [Required], [Total])
	select @Id, dc.[Name], dc.[DataType], dc.[MaxLength],
		Reference = case dc.Ref 
			when N'self' then @Id
			when N'parent' then @Parent
			else null
		end,
		dc.[Role], dc.[Order], dc.[Required], dc.[Total]
	from a2meta.DefaultColumns dc
	where [Schema] = @Schema and Kind = @Kind
	order by [Order];

	insert into a2meta.MenuItems(Interface, [Name], [Order])
	select @Id, [Name], [Order] from a2meta.DefaultSections where [Schema] = @Schema;

	select [Elem!TElem!Object] = null, [Id!!Id] = Id, IsFolder, Kind, [Schema], [Name]
	from a2meta.[Catalog] where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2meta.[Config.DeleteItem]
@UserId bigint,
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	-- table and its details
	declare @tables a2sys.[Guid.TableType];
	insert into @tables(Id)
	select Id from a2meta.[Catalog] where Id = @Id or Parent = @Id;

	begin tran;

	delete from a2meta.ApplyMapping
	from a2meta.ApplyMapping am
		inner join a2meta.Columns c on c.Id in (am.Source, am.[Target])
		inner join @tables t on t.Id in (c.[Table], c.[Reference]);

	delete from a2meta.ApplyMapping
	from a2meta.ApplyMapping am
		inner join a2meta.Apply a on am.Apply = a.Id
		inner join @tables t on t.Id in (a.Details);

	delete from a2meta.Apply
	from a2meta.Apply a inner join @tables t on t.Id in (a.Details);

	delete from a2meta.Columns 
	from a2meta.Columns c inner join @tables t on t.Id in (c.[Table], c.[Reference])

	delete from a2meta.[Catalog] 
	from a2meta.[Catalog] c inner join @tables t on c.Id = t.Id

	commit tran;
			
end
go
------------------------------------------------
create or alter procedure a2meta.[Application.Load]
@UserId bigint,
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	
	select [App!TApp!Object] = null, [Id!!Id] = Id, [Name!!Name] = [Name], Title, IdDataType,
		[Memo], [Version]
	from a2meta.[Application] where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2meta.[Table.Load]
@UserId bigint,
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Table!TTable!Object] = null, [Id!!Id] = t.Id, [Name!!Name] = t.[Name], t.[Schema], t.Kind, t.[Type],
		t.ItemsName, t.ItemName, t.TypeName, t.EditWith, t.ItemLabel, t.ItemsLabel, t.Source,  t.UseFolders, t.FolderMode,
		[ParentTable.Id!TPTable!Id] = t.ParentTable, [ParentTable.Name!TPTable!Name] = a2meta.fn_TableFullName(p.[Schema], p.[Name]),
		[ParentTable.Endpoint!TPTable!] = lower(a2meta.fn_Schema2Text(p.[Schema]) + '/' + p.[Name]),
		[ParentElem.Schema!TPElem!] = x.[Schema], [ParentElem.Name!TPElem!] = x.[Name],
		[Columns!TColumn!Array] = null,
		[Apply!TApply!Array] = null,
		[Kinds!TKind!Array] = null,
		[Endpoint] = lower(a2meta.fn_Schema2Text(t.[Schema]) + '/' + t.[Name])
	from a2meta.[Catalog] t 
		left join a2meta.[Catalog] p on t.ParentTable = p.Id
		left join a2meta.[Catalog] x on t.Parent = x.Id and x.Kind <> N'folder'
	where t.Id = @Id;
	
	select [!TColumn!Array] = null, [Id!!Id] = c.Id, [Name!!Name] = c.[Name], c.[Label],
		c.DataType, c.[MaxLength], c.[Role], c.Source, c.Computed, c.[Required], c.[Total], c.[Unique],
		[Reference.Id!TRef!Id] = c.Reference, [Reference.Name!TRef!Name] = a2meta.fn_TableFullName(rt.[Schema], rt.[Name]),
		[Order!!RowNumber] = c.[Order],
		[!TTable.Columns!ParentId] = @Id
	from a2meta.Columns c 
		left join a2meta.[Catalog] rt on c.Reference = rt.Id 
	where c.[Table] = @Id
	order by c.[Order];

	select [!TKind!Array] = null, [Id!!Id] = Id, [Name], [Label],
		[Order!!RowNumber] = [Order],
		[!TTable.Kinds!ParentId] = Details
	from a2meta.[DetailsKinds]
	where [Details] = @Id
	order by [Order];

	select [!TApply!Array] = null, [Id!!Id] = a.Id, a.[InOut], a.Storno,
		[RowNo!!RowNumber] = a.[Order], 
		[Kind.Id!TKind!Id] = a.[Kind], [Kind.Name!TKind!Name] = dk.[Name],
		[Journal.Id!TRef!Id] = a.Journal, [Journal.Name!TRef!Name] = a2meta.fn_TableFullName(j.[Schema], j.[Name]),
		[Details.Id!TRef!Id] = a.Details, [Details.Name!TRef!Name] = a2meta.fn_TableFullName(d.[Schema], d.[Name]),
		[!TTable.Apply!ParentId] = @Id
	from a2meta.[Apply] a
		left join a2meta.[Catalog] j on a.Journal = j.Id
		left join a2meta.[Catalog] d on a.Details = d.Id
		left join a2meta.DetailsKinds dk on a.Kind = dk.Id
	where a.[Table] = @Id
	order by a.[Order];
end
go
------------------------------------------------
drop procedure if exists a2meta.[Table.Metadata];
drop procedure if exists a2meta.[Table.Update];
drop procedure if exists a2meta.[Report.Metadata];
drop procedure if exists a2meta.[Report.Update];
drop procedure if exists a2meta.[Enum.Metadata];
drop procedure if exists a2meta.[Enum.Update];
drop procedure if exists a2meta.[Application.Metadata];
drop procedure if exists a2meta.[Application.Update];
drop type if exists a2meta.[App.TableType];
drop type if exists a2meta.[Table.TableType];
drop type if exists a2meta.[Table.Column.TableType];
drop type if exists a2meta.[Table.Kind.TableType];
drop type if exists a2meta.[Table.Apply.TableType];
drop type if exists a2meta.[Table.RepItem.TableType];
drop type if exists a2meta.[Table.EnumItem.TableType];
go
------------------------------------------------
create type a2meta.[App.TableType] as table (
	[Id] uniqueidentifier,
	[Name] nvarchar(255),
	[Title] nvarchar(255),
	IdDataType nvarchar(32),
	Memo nvarchar(255)
);
go
------------------------------------------------
create type a2meta.[Table.TableType] as table (
	Id uniqueidentifier,
	[Name] nvarchar(128),
	[ItemsName] nvarchar(128),
	[ItemName] nvarchar(128),
	[TypeName] nvarchar(128),
	EditWith nvarchar(16),
	ParentTable uniqueidentifier,
	ItemLabel nvarchar(255),
	ItemsLabel nvarchar(255),
	UseFolders bit,
	FolderMode nvarchar(16),
	[Type] nvarchar(32)
);
go
------------------------------------------------
create type a2meta.[Table.Column.TableType] as table (
	Id uniqueidentifier,
	[Name] nvarchar(128),
	[Label] nvarchar(255),
	[DataType] nvarchar(32),
	[MaxLength] int,
	[Modifier] nvarchar(32),
	[Role] bigint,
	Reference uniqueidentifier,
	[Order] int,
	[Computed] nvarchar(255),
	[Required] bit,
	[Total] bit,
	[Unique] bit
);
go
------------------------------------------------
create type a2meta.[Table.RepItem.TableType] as table (
	Id uniqueidentifier,
	[Label] nvarchar(255),
	[Column] uniqueidentifier,
	[Order] int,
	[Func] nvarchar(32),
	[Checked] bit
);
go
------------------------------------------------
create type a2meta.[Table.EnumItem.TableType] as table (
	Id uniqueidentifier,
	[Name] nvarchar(16) not null,
	[Label] nvarchar(255),
	[Order] int not null,
	[Inactive] bit
);
go
------------------------------------------------
create type a2meta.[Table.Kind.TableType] as table (
	[Id] uniqueidentifier null,
	[Order] int null,
	[Name] nvarchar(32),
	[Label] nvarchar(255)
);
go
------------------------------------------------
create type a2meta.[Table.Apply.TableType] as table (
	[Id] uniqueidentifier,
	[Journal] uniqueidentifier,
	[RowNo] int null,
	Details uniqueidentifier,
	[InOut] smallint,
	Storno bit,
	Kind uniqueidentifier
);
go
------------------------------------------------
create or alter procedure a2meta.[Table.Metadata]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @Table a2meta.[Table.TableType];
	declare @Columns a2meta.[Table.Column.TableType];
	declare @Apply a2meta.[Table.Apply.TableType];
	declare @Kinds a2meta.[Table.Kind.TableType];

	select [Table!Table!Metadata] = null, * from @Table;
	select [Columns!Table.Columns!Metadata] = null, * from @Columns;
	select [Apply!Table.Apply!Metadata] = null, * from @Apply;
	select [Kinds!Table.Kinds!Metadata] = null, * from @Kinds;
end
go
------------------------------------------------
create or alter procedure a2meta.[Table.Update]
@UserId bigint,
@Table a2meta.[Table.TableType] readonly,
@Columns a2meta.[Table.Column.TableType] readonly,
@Apply a2meta.[Table.Apply.TableType] readonly,
@Kinds a2meta.[Table.Kind.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @Id uniqueidentifier;
	select @Id = Id from @Table;

	merge a2meta.[Catalog] as t
	using @Table as s on
	t.Id = s.Id
	when matched then update set
		t.[Name] = s.[Name],
		t.[ItemsName] = s.[ItemsName],
		t.[ItemName] = s.[ItemName],
		t.[TypeName] = s.[TypeName],
		t.EditWith = s.EditWith,
		t.ParentTable = s.ParentTable,
		t.ItemLabel = s.ItemLabel,
		t.ItemsLabel = s.ItemsLabel,
		t.UseFolders = s.UseFolders,
		t.FolderMode = s.FolderMode,
		t.[Type] = s.[Type];

	merge a2meta.[Columns] as t
	using @Columns as s
	on t.Id = s.Id
	when matched then update set
		t.[Name] = s.[Name],
		t.[Label] = s.[Label],
		t.DataType = s.[DataType],
		t.[MaxLength] = s.[MaxLength],
		t.Reference = s.Reference,
		t.[Role] = s.[Role],
		t.[Order] = s.[Order],
		t.Computed = s.Computed,
		t.[Required] = s.[Required],
		t.[Total] = s.[Total],
		t.[Unique] = s.[Unique]
	when not matched then insert
		([Table], [Name], [Label], DataType, [MaxLength], Reference, [Role], [Order], 
			Computed, [Required], [Total], [Unique]) values
		(@Id, s.[Name], s.[Label], s.[DataType], s.[MaxLength], s.Reference, s.[Role], s.[Order], 
			s.Computed, s.[Required], s.[Total], s.[Unique])
	when not matched by source and t.[Table] = @Id then delete;

	merge a2meta.[Apply] as t
	using @Apply as s
	on t.Id = s.Id
	when matched then update set
		t.Journal = s.Journal,
		t.Details = s.Details,
		t.InOut = s.InOut,
		t.Storno = s.Storno,
		t.[Order] = s.RowNo,
		t.Kind = s.Kind
	when not matched then insert
		([Table], Journal, Details, InOut, [Order], Storno, Kind) values
		(@Id, s.Journal, s.Details, s.InOut, s.RowNo, s.Storno, s.Kind)
	when not matched by source and t.[Table] = @Id then delete;

	merge a2meta.[DetailsKinds] as t
	using @Kinds as s
	on t.Id = s.Id
	when matched then update set
		t.[Name] = s.[Name],
		t.[Label] = s.[Label],
		t.[Order] = s.[Order]
	when not matched then insert
		([Details], [Name], [Label], [Order]) values
		(@Id, s.[Name], s.[Label], s.[Order])
	when not matched by source and t.[Details] = @Id then delete;

	exec a2meta.[Table.Load] @UserId, @Id;
end
go
------------------------------------------------
create or alter procedure a2meta.[Report.Load]
@UserId bigint,
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Table!TTable!Object] = null, [Id!!Id] = t.Id, [Name!!Name] = t.[Name], t.[Schema], t.Kind,
		t.ItemsName, t.ItemName, t.Source, t.ItemsLabel, t.ItemLabel, t.[Type],
		[ParentTable.Id!TPTable!Id] = t.ParentTable, [ParentTable.Name!TPTable!Name] = a2meta.fn_TableFullName(p.[Schema], p.[Name]),
		[ParentTable.Endpoint!TPTable!] = lower(a2meta.fn_Schema2Text(p.[Schema]) + '/' + p.[Name]),
		[Endpoint] = lower(a2meta.fn_Schema2Text(t.[Schema]) + '/' + t.[Name]),
		[Filters!TRepItem!Array] = null,
		[Grouping!TRepItem!Array] = null,
		[Data!TRepItem!Array] = null
	from a2meta.[Catalog] t 
		left join a2meta.[Catalog] p on t.ParentTable = p.Id
	where t.Id = @Id;

	select [!TRepItem!Array] = null, [Id!!Id] = ri.Id, ri.Checked, ri.Func, ri.[Label],
		[Column.Id!TColumn!Id] = c.Id, [Column.Name!TColumn!Name] = t.[Name] + N'.' + c.[Name],
		[Column.Field!TColumn!] = c.[Name], [Column.DataType!TColumn!] = c.[DataType],
		[Order!!RowNumber] = ri.[Order],
		[!TTable.Filters!ParentId] = case when ri.Kind = N'F' then ri.Report else null end,
		[!TTable.Grouping!ParentId] = case when ri.Kind = N'G' then ri.Report else null end,
		[!TTable.Data!ParentId] = case when ri.Kind = N'D' then ri.Report else null end
	from a2meta.ReportItems ri
		inner join a2meta.Columns c on ri.[Column] = c.Id
		inner join a2meta.[Catalog] t on c.[Table] = t.Id
	where ri.Report = @Id
	order by ri.[Order];
end
go
------------------------------------------------
create or alter procedure a2meta.[Report.Metadata]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @Table a2meta.[Table.TableType];
	declare @RepItem a2meta.[Table.RepItem.TableType];

	select [Table!Table!Metadata] = null, * from @Table;
	select [Grouping!Table.Grouping!Metadata] = null, * from @RepItem;
	select [Filters!Table.Filters!Metadata] = null, * from @RepItem;
	select [Data!Table.Data!Metadata] = null, * from @RepItem;
end
go
------------------------------------------------
create or alter procedure a2meta.[Report.Update]
@UserId bigint,
@Table a2meta.[Table.TableType] readonly,
@Grouping a2meta.[Table.RepItem.TableType] readonly,
@Filters a2meta.[Table.RepItem.TableType] readonly,
@Data a2meta.[Table.RepItem.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @Id uniqueidentifier;
	select @Id = Id from @Table;

	merge a2meta.[Catalog] as t
	using @Table as s on
	t.Id = s.Id
	when matched then update set
		t.[Name] = s.[Name],
		t.ParentTable = s.ParentTable,
		t.ItemLabel = s.ItemLabel,
		t.[Type] = s.[Type];

	with TI as (
		select Kind = 'F', * from @Filters
		union all
		select Kind = 'G', * from @Grouping
		union all
		select Kind = 'D', * from @Data		
	)
	merge a2meta.ReportItems as t
	using TI as s
	on t.Id = s.Id and t.Report = @Id
	when matched then update set
		t.[Label] = s.[Label],
		t.Checked = s.Checked,
		t.[Order] = s.[Order],
		t.Func = s.Func
	when not matched then insert
		 (Report, [Column], Kind, [Order], [Label], Func, Checked) values
		 (@Id, s.[Column], s.Kind, s.[Order], s.[Label], s.Func, s.Checked)
	when not matched by source and t.Report = @Id then delete;

	exec a2meta.[Report.Load] @UserId, @Id;
end
go
------------------------------------------------
create or alter procedure a2meta.[Enum.Load]
@UserId bigint,
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Table!TTable!Object] = null, [Id!!Id] = t.Id, [Name!!Name] = t.[Name], t.[Schema], t.Kind,
		t.ItemsName, t.ItemName, t.Source, t.ItemsLabel, t.ItemLabel, t.[Type],
		[Items!TEnumItem!Array] = null
	from a2meta.[Catalog] t 
		left join a2meta.[Catalog] p on t.ParentTable = p.Id
	where t.Id = @Id;

	select [!TEnumItem!Array] = null, [Id!!Id] = ei.Id, ei.[Name], ei.[Label], 
		[Order!!RowNumber] = ei.[Order],
		[!TTable.Items!ParentId] = ei.Enum
	from a2meta.EnumItems ei
	where ei.Enum = @Id
	order by ei.[Order];
end
go

------------------------------------------------
create or alter procedure a2meta.[Enum.Metadata]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @Table a2meta.[Table.TableType];
	declare @EnumItem a2meta.[Table.EnumItem.TableType];

	select [Table!Table!Metadata] = null, * from @Table;
	select [Items!Table.Items!Metadata] = null, * from @EnumItem;
end
go
------------------------------------------------
create or alter procedure a2meta.[Enum.Update]
@UserId bigint,
@Table a2meta.[Table.TableType] readonly,
@Items a2meta.[Table.EnumItem.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @Id uniqueidentifier;
	select @Id = Id from @Table;

	merge a2meta.[Catalog] as t
	using @Table as s on
	t.Id = s.Id
	when matched then update set
		t.[Name] = s.[Name];

	merge a2meta.EnumItems as t
	using @Items as s
	on t.Id = s.Id and t.Enum = @Id
	when matched then update set
		t.[Name] = s.[Name],
		t.[Label] = s.[Label],
		t.[Order] = s.[Order],
		t.Inactive = s.Inactive
	when not matched then insert
		 (Enum, [Name], [Label], [Order], Inactive) values
		 (@Id, s.[Name], s.[Label], s.[Order], s.Inactive)
	when not matched by source and t.Enum = @Id then delete;

	exec a2meta.[Enum.Load] @UserId, @Id;
end
go
------------------------------------------------
create or alter procedure a2meta.[Application.Metadata]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	declare @App a2meta.[App.TableType];

	select [App!App!Metadata] = null, * from @App;
end
go
------------------------------------------------
create or alter procedure a2meta.[Application.Update]
@UserId bigint,
@App a2meta.[App.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @Id uniqueidentifier;
	select @Id = Id from @App;

	update a2meta.[Application] set [Name] = s.[Name], [Title] = s.Title,
		IdDataType = s.IdDataType, Memo = s.[Memo]
	from a2meta.[Application] t inner join @App s on  t.Id = s.Id

	exec a2meta.[Application.Load] @UserId, @Id;
end
go
------------------------------------------------
create or alter procedure a2meta.[Reference.Index]
@UserId bigint,
@Id uniqueidentifier = null,
@Fragment nvarchar(255) = null,
@Schema nvarchar(32) = null,
@DataType nvarchar(32) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @fr nvarchar(255) = N'%' + @Fragment + N'%';

	select [Tables!TRefTable!Array] = null, [Id!!Id] = Id, [Name!!Name] = a2meta.fn_TableFullName([Schema], [Name]),
		[Schema] = a2meta.fn_Schema2Text([Schema]), TableName = [Name]
	from a2meta.[Catalog] where 
		(
			@DataType = N'reference' and Kind in (N'table', N'details')			
			or @DataType = N'enum' and Kind in (N'enum')
		)
		and (@Schema is null or [Schema] = @Schema)
		and (@fr is null or [Name] like @fr)
	order by [Name];		
end
go
------------------------------------------------
create or alter procedure a2meta.[Reference.Fetch]
@UserId bigint,
@Text nvarchar(255),
@Schema nvarchar(32) = null,
@DataType nvarchar(32) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @fr nvarchar(255) = N'%' + @Text + N'%';

	select [Tables!TRefTable!Array] = null, [Id!!Id] = Id, [Name!!Name] = a2meta.fn_TableFullName([Schema], [Name])
	from a2meta.[Catalog] where 
		(@DataType = N'reference' and Kind in (N'table', N'details')
			or @DataType = N'enum' and Kind in (N'enum'))
		and [Name] like @fr
		and (@Schema is null or [Schema] = @Schema)
	order by [Name];		
end
go
------------------------------------------------
create or alter procedure a2meta.[Details.Index] 
@UserId bigint,
@Id uniqueidentifier = null,
@Text nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @fr nvarchar(255) = N'%' + @Text + N'%';

	select [Tables!TRefTable!Array] = null, [Id!!Id] = Id, [Name!!Name] = a2meta.fn_TableFullName([Schema], [Name])
	from a2meta.[Catalog] where Kind in (N'details') 
		and (@fr is null or [Name] like @fr)
	order by [Name];		
end
go

------------------------------------------------
create or alter procedure a2meta.[DetailsKind.Index] 
@UserId bigint,
@Id uniqueidentifier = null,
@Details uniqueidentifier,
@Text nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @fr nvarchar(255) = N'%' + @Text + N'%';

	select [Kinds!TKind!Array] = null, [Id!!Id] = Id, [Name!!Name] = [Name]
	from a2meta.DetailsKinds where Details = @Details
		and (@fr is null or [Name] like @fr)
	order by [Order];
end
go
------------------------------------------------
create or alter procedure a2meta.[Deploy.Index]
@UserId bigint,
@Id uniqueidentifier = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @count int;
	select @count = count(*) from a2meta.[Catalog]
	where IsFolder = 0 and [Kind] in (N'table', N'details');

	declare @tmp table([Name] nvarchar(255), [Kind] nvarchar(32));
	insert into @tmp ([Kind], [Name]) values
	(N'Table', N'Tables'),
	(N'TableType', N'Table Types'),
	(N'ForeignKey', N'Foreign Keys');

	select [Deploy!TDeploy!Array] = null, [Name], Kind, [Count] = @count,
		[TableName] = cast(null as nvarchar(255)), [Index] = 0,
		[Table] = cast(null as nvarchar(255))
	from @tmp
end
go
------------------------------------------------
create or alter procedure a2meta.[Apply.Mapping.Auto]
@Id uniqueidentifier,
@Table uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @jrnTable uniqueidentifier, @detailsTable uniqueidentifier;

	select @jrnTable = Journal, @detailsTable = Details 
	from a2meta.Apply
	where Id = @Id;

	declare @maps table(TargetId uniqueidentifier, TargetName nvarchar(255), TargetRef uniqueidentifier,
		[SourceId] uniqueidentifier, SourceName nvarchar(255), [Order] int);

	insert into @maps(TargetId, TargetName, [Order], TargetRef)
	select Id, [Name], [Order], Reference from a2meta.Columns 
	where [Table] = @jrnTable and [Name] not in (N'Id', 'InOut');

	declare @TablePK uniqueidentifier
	select @TablePK = Id from a2meta.Columns c
	where c.[Table] = @Table and [Role] = 1;

	update @maps set SourceId = @TablePK
	where TargetRef = @Table;

	-- base table first
	update @maps set SourceId = c.Id
	--select c.Id, * 
	from @maps m inner join a2meta.Columns c on m.[TargetName] = c.[Name] -- and MatchType(c.DataType)
	where c.[Table] = @Table;

	if @detailsTable is not null
		update @maps set SourceId = c.Id
		--select c.Id, * 
		from @maps m inner join a2meta.Columns c on m.[TargetName] = c.[Name] -- and MatchType(c.DataType)
		where c.[Table] = @detailsTable;

	insert into a2meta.ApplyMapping (Apply, [Target], Source)
	select Apply = @Id, [Target] = TargetId, [Source] = SourceId
		--[RowNo] = row_number() over (order by [Order])
	from @maps where SourceId is not null
	order by [Order];
end
go
------------------------------------------------
create or alter procedure a2meta.[Mapping.Load]
@UserId bigint,
@Id uniqueidentifier = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @opTable uniqueidentifier;
	select @opTable = c.ParentTable
	from a2meta.[Apply] a inner join a2meta.[Catalog] c on a.[Table] = c.Id
	where a.Id = @Id;

	if not exists(select * from a2meta.ApplyMapping where Apply = @Id)
		exec a2meta.[Apply.Mapping.Auto] @Id, @opTable;

	select [Apply!TApply!Object] = null, [Id!!Id] = @Id;

	select [Mapping!TMapping!Array] = null, [Id!!Id] = a.Id,
		[Target.Id!TColumn!Id] = a.[Target], [Target.Name!TColumn!Name] = ct.[Name], [Target.DataType!TColumn!] = ct.DataType,
		[Source.Id!TColumn!Id] = a.[Source], [Source.Name!TColumn!Name] = cs.[Name], [Source.DataType!TColumn!] = cs.DataType,
		[TargetTable!TTable!RefId] = ct.[Table],
		[SourceTable!TTable!RefId] = cs.[Table]
	from a2meta.ApplyMapping a
		inner join a2meta.Columns cs on a.Source = cs.Id
		inner join a2meta.Columns ct on a.[Target] = ct.Id
	where Apply = @Id
	order by a.[Id];

	select [Tables!TTable!Map] = null, [Id!!Id] = c.Id, 
		[Name] = a2meta.fn_TableFullName(c.[Schema], c.[Name]),		
		IsJournal = cast(case when c.[Schema] = N'jrn' then 1 else 0 end as bit),
		[Columns!TColumn!Array] = null
	from a2meta.[Catalog] c
		inner join a2meta.Apply a on c.Id in (a.Details, a.Journal, @opTable)
	where a.Id = @Id;

	select [!TColumn!Array] = null, [Id!!Id] = c.Id, [Name!!Name] = c.[Name], c.DataType,
		[!TTable.Columns!ParentId] = c.[Table]
	from a2meta.Columns c
		inner join a2meta.Apply a on c.[Table] in (a.Details, a.Journal, @opTable)
	where a.Id = @Id
	order by c.Id;
end
go
------------------------------------------------
drop procedure if exists a2meta.[Mapping.Metadata];
drop procedure if exists a2meta.[Mapping.Update];
drop type if exists a2meta.[Mapping.TableType];
go
------------------------------------------------
create type a2meta.[Mapping.TableType] as table (
	Id uniqueidentifier,
	Source uniqueidentifier,
	[Target] uniqueidentifier
);
go
------------------------------------------------
create or alter procedure a2meta.[Mapping.Metadata]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	declare @Mapping a2meta.[Mapping.TableType];
	select [Mapping!Mapping!Metadata] = null, * from @Mapping;
end
go
------------------------------------------------
create or alter procedure a2meta.[Mapping.Update]
@UserId bigint,
@Id nvarchar(255), -- PLATFORM FEATURE
@Mapping a2meta.[Mapping.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @ApplyId uniqueidentifier;
	set @ApplyId = cast(@Id as uniqueidentifier);

	merge a2meta.ApplyMapping as t
	using @Mapping as s
	on t.Id = s.Id and t.Apply = @ApplyId
	when matched then update set
		t.[Source] = s.Source,
		t.[Target] = s.[Target]
	when not matched then insert
		(Apply, [Target], Source) values
		(@Id, s.[Target], s.Source)
	when not matched by source and t.Apply = @Id then delete;

	exec a2meta.[Mapping.Load] @UserId = @UserId, @Id = @ApplyId;
end
go

------------------------------------------------
create or alter procedure a2meta.[Interface.Load]
@UserId bigint,
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Interface!TInterface!Object] = null, [Id!!Id] = t.Id, [Name!!Name] = t.[Name], 
		t.[Schema],
		t.Kind, t.ItemsName, t.ItemName, t.Source, t.TypeName,
		[Sections!TSection!Array] = null
	from a2meta.[Catalog] t 
		left join a2meta.[Catalog] p on t.ParentTable = p.Id
	where t.Id = @Id;

	select [!TSection!Array] = null, [Id!!Id] = Id, [Name], [Url],
		[Order!!RowNumber] = [Order],
		[MenuItems!TMenuItem!Array] = null,
		[!TInterface.Sections!ParentId] = Interface
	from a2meta.MenuItems where [Interface] = @Id and Parent is null
	order by [Order];

	select [!TMenuItem!Array] = null, [Id!!Id] = Id, [Name], [Url],
		[Order!!RowNumber] = [Order],
		[!TSection.MenuItems!ParentId] = Parent
	from a2meta.MenuItems where [Interface] = @Id and Parent is not null
	order by [Order];
end
go


------------------------------------------------
drop procedure if exists a2meta.[Interface.Metadata];
drop procedure if exists a2meta.[Interface.Update];
drop type if exists a2meta.[Interface.TableType];
drop type if exists a2meta.[Interface.Section.TableType];
go
------------------------------------------------
create type a2meta.[Interface.TableType] as table (
	Id uniqueidentifier,
	[Name] nvarchar(255),
	ItemName nvarchar(255) -- icon
);
go
------------------------------------------------
create type a2meta.[Interface.Section.TableType] as table (
	[GUID] uniqueidentifier,
	[ParentGUID] uniqueidentifier,
	Id uniqueidentifier,
	[Name] nvarchar(255),
	[Url] nvarchar(255),
	[Order] int
);
go
------------------------------------------------
create or alter procedure a2meta.[Interface.Metadata]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	declare @Interface a2meta.[Interface.TableType];
	declare @Sections a2meta.[Interface.Section.TableType];
	select [Interface!Interface!Metadata] = null, * from @Interface;
	select [Sections!Interface.Sections!Metadata] = null, * from @Sections;
	select [MenuItems!Interface.Sections.MenuItems!Metadata] = null, * from @Sections;
end
go
------------------------------------------------
create or alter function a2meta.LocalizeName(@Name nvarchar(255))
returns nvarchar(255)
as
begin
	if @Name is null or len(@Name) < 1
		return @Name;
	if N'@' = substring(@Name, 1, 1) 
		return N'@[' + substring(@Name, 2, 255) + N']';
	return @Name;
end
go
------------------------------------------------
create or alter procedure a2meta.[Interface.Deploy] 
@UserId bigint, 
@Id uniqueidentifier
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @emptyGuid uniqueidentifier = cast(N'00000000-0000-0000-0000-000000000000' as uniqueidentifier);

	with TR as (
		select [Id], [Name] = a2meta.LocalizeName([Name]), Icon = ItemName, [Order] 
		from a2meta.[Catalog] where Id = @Id
	)
	merge a2ui.Menu as t
	using TR as s
	on t.Id = s.Id
	when matched then update set
		t.[Name] = s.[Name],
		t.[Icon] = s.[Icon],
		t.[Order] = s.[Order]
	when not matched then insert
		(Id, Parent, [Name], [Icon], [Order]) values
		(@Id, @emptyGuid, s.[Name], s.[Icon], s.[Order]);		
	
	with TF as (
		select Id, Interface, Parent, [Name] = a2meta.LocalizeName([Name]), [Url], [Order]
		from a2meta.MenuItems where Interface = @Id
	)
	merge a2ui.Menu as t
	using TF as s
	on t.Id = s.Id
	when matched then update set 
		t.[Name] = s.[Name],
		t.[Order] = s.[Order],
		t.[Url] =  N'page:/' + s.[Url] + N'/index/0'
	when not matched then insert 
		(Id, Parent, [Name], [Order], 
			[Url]) values
		(s.Id, isnull(s.Parent, @Id), s.[Name], s.[Order],
			N'page:/' + s.[Url] + N'/index/0');
end
go
------------------------------------------------
create or alter procedure a2meta.[Interface.Update]
@UserId bigint,
@Interface a2meta.[Interface.TableType] readonly,
@Sections a2meta.[Interface.Section.TableType] readonly,
@MenuItems a2meta.[Interface.Section.TableType] readonly
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @Id uniqueidentifier;
	select @Id = Id from @Interface;

	merge a2meta.[Catalog] as t
	using @Interface as s on t.Id = s.Id
	when matched then update set
		t.[Name] = s.[Name],
		t.ItemName = s.[ItemName];

	declare @rmenu table(Id uniqueidentifier, [GUID] uniqueidentifier);

	merge a2meta.[MenuItems] as t
	using @Sections as s
	on t.Id = s.Id and t.Interface = @Id and t.Parent is null
	when matched then update set
		t.[Name] = s.[Name],
		t.[Order] = s.[Order]
	when not matched then insert
		(Interface, Parent, [Name], [Order]) values
		(@Id, null, s.[Name], s.[Order])
	when not matched by source and t.Interface = @Id and t.Parent is null then delete
	output inserted.Id, s.[GUID] into @rmenu(Id, [GUID]);

	with TT as (
		select * from a2meta.MenuItems where Interface = @Id and Parent is not null
	),
	TS as (
		select mi.Id, Parent = rm.Id, mi.[Name], mi.[Url], mi.[Order]
		from @MenuItems mi
		inner join @rmenu rm on mi.ParentGUID = rm.[GUID]
	)
	merge TT as t
	using TS as s
	on t.Id = s.Id and t.Interface = @Id
	when matched then update set
		t.[Name] = s.[Name],
		t.[Url] = s.[Url],
		t.[Order] = s.[Order]
	when not matched then insert
		(Interface, Parent, [Name], [Url], [Order]) values
		(@Id, s.Parent, s.[Name], s.[Url], s.[Order])
	when not matched by source and t.Interface = @Id and t.Parent is not null then delete;

	exec a2meta.[Interface.Deploy] @UserId, @Id;

	exec a2meta.[Interface.Load] @UserId, @Id;
end
go
------------------------------------------------
create or alter procedure a2meta.[Journal.Field.Index] 
@UserId bigint,
@Id uniqueidentifier = null,
@Journal uniqueidentifier,
@Kind nchar(1)
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Columns!TColumn!Array] = null, [Id!!Id] = c.Id, Field = c.[Name], c.[DataType],
		[Name] = t.[Name] + N'.' + c.[Name]
	from a2meta.[Columns] c
	inner join a2meta.[Catalog] t on c.[Table] = t.Id
	where [Table] = @Journal and c.[Name] not in (N'Id', N'InOut')
	order by c.[Order];
end
go

------------------------------------------------
create or alter procedure a2meta.[Endpoint.Index] 
@UserId bigint,
@Id uniqueidentifier = null,
@Fragment nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @fr nvarchar(255) = N'%' + @Fragment + N'%';

	select [Endpoints!TEndpoint!Array] = null, [Schema] = a2meta.fn_Schema2Text(t.[Schema]),
		[Name] = isnull(isnull(ItemsLabel, ItemLabel), isnull(N'@' + ItemsName, N'@' + [Name])), [Endpoint] = lower(a2meta.fn_Schema2Text(t.[Schema]) + '/' + t.[Name])
	from a2meta.[Catalog] t
	where t.IsFolder = 0 and Kind in (N'table', N'operation', N'report')
		and (@fr is null or t.[Name] like @fr or t.ItemsName like @fr or t.ItemsLabel like @fr)
	order by [Schema];
end
go




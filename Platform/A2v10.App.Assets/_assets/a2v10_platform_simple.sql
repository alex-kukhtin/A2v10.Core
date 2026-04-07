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


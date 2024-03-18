/*
Copyright © 2008-2024 Oleksandr Kukhtin

Last updated : 17 mar 2024
module version : 8267
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
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2security' and SEQUENCE_NAME=N'SQ_Tenants')
	create sequence a2security.SQ_Tenants as bigint start with 100 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2security' and TABLE_NAME=N'Tenants')
create table a2security.Tenants
(
	Id	int not null constraint PK_Tenants primary key
		constraint DF_Tenants_PK default(next value for a2security.SQ_Tenants),
	[Admin] bigint null, -- admin user ID
	[Source] nvarchar(255) null,
	[TransactionCount] bigint not null constraint DF_Tenants_TransactionCount default(0),
	LastTransactionDate datetime null,
	UtcDateCreated datetime not null constraint DF_Tenants_UtcDateCreated default(getutcdate()),
	TrialPeriodExpired datetime null,
	DataSize float null,
	[State] nvarchar(128) null,
	UserSince datetime null,
	LastPaymentDate datetime null,
	[Locale] nvarchar(32) not null constraint DF_Tenants_Locale default(N'uk-UA')
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
		constraint DF_Users_PK default(next value for a2security.SQ_Users),
	Tenant int not null 
		constraint FK_Users_Tenant_Tenants foreign key references a2security.Tenants(Id),
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
	Segment nvarchar(32) null,
	SetPassword bit,
	IsApiUser bit constraint DF_UsersIsApiUser default(0),
	IsExternalLogin bit constraint DF_UsersIsExternalLogin default(0),
	IsBlocked bit constraint DF_UsersIsBlocked default(0),
	UtcDateCreated datetime not null constraint DF_Users_UtcDateCreated default(getutcdate()),
	constraint PK_Users primary key (Tenant, Id)
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
	[User] bigint not null,
	Tenant int not null, 
	[Mode] nvarchar(16) not null, -- ApiKey, OAuth2, JWT
	[ClientId] nvarchar(255),
	[ClientSecret] nvarchar(255),
	[ApiKey] nvarchar(1023),
	[AllowIP] nvarchar(1024),
	Memo nvarchar(255),
	RedirectUrl nvarchar(255),
	[UtcDateModified] datetime not null constraint DF_ApiUserLogins_DateModified default(getutcdate()),
	constraint PK_ApiUserLogins primary key clustered ([User], Tenant, Mode) with (fillfactor = 70),
	constraint FK_ApiUserLogins_User_Users foreign key (Tenant, [User]) references a2security.Users(Tenant, Id)

);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2security' and TABLE_NAME=N'ExternalUserLogins')
create table a2security.ExternalUserLogins
(
	[User] bigint not null,
	Tenant int not null 
		constraint FK_ExternalUserLogins_Tenant_Tenants foreign key references a2security.Tenants(Id),
	[LoginProvider] nvarchar(255) not null,
	[ProviderKey] nvarchar(max) not null,
	[UtcDateModified] datetime not null constraint DF_ExternalUserLogins_DateModified default(getutcdate()),
	constraint PK_ExternalUserLogins primary key clustered ([User], Tenant, LoginProvider) with (fillfactor = 70),
	constraint FK_ExternalUserLogins_User_Users foreign key (Tenant, [User]) references a2security.Users(Tenant, Id)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2security' and TABLE_NAME=N'DeletedUsers')
create table a2security.DeletedUsers
(
	Id	bigint not null,
	Tenant int not null 
		constraint FK_DeletedUsers_Tenant_Tenants foreign key references a2security.Tenants(Id),
	UserName nvarchar(255) not null,
	DomainUser nvarchar(255) null,
	Email nvarchar(255) null,
	PhoneNumber nvarchar(255) null,
	DeletedByUser bigint not null,
	UtcDateDeleted datetime not null constraint DF_Users_UtcDateDeleted default(getutcdate()),
	constraint PK_DeletedUsers primary key (Tenant, Id),
	constraint FK_DeletedUsers_DeletedByUser_Users foreign key (Tenant, DeletedByUser) references a2security.Users(Tenant, Id)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2security' and TABLE_NAME=N'RefreshTokens')
create table a2security.RefreshTokens
(
	UserId bigint not null,
	Tenant int not null 
		constraint FK_RefreshTokens_Tenant_Tenants foreign key references a2security.Tenants(Id),
	[Provider] nvarchar(64) not null,
	[Token] nvarchar(255) not null,
	Expires datetime not null,
		constraint FK_RefreshTokens_UserId_Users foreign key (Tenant, UserId) references a2security.Users(Tenant, Id),
	constraint PK_RefreshTokens primary key (Tenant, UserId, [Provider], Token) with (fillfactor = 70),
);
go
------------------------------------------------
create or alter view a2security.ViewUsers
as
	select Id, UserName, DomainUser, PasswordHash, SecurityStamp, Email, PhoneNumber,
		LockoutEnabled, AccessFailedCount, LockoutEndDateUtc, TwoFactorEnabled, [Locale],
		PersonName, Memo, Void, LastLoginDate, LastLoginHost, Tenant, EmailConfirmed,
		PhoneNumberConfirmed, RegisterHost, ChangePasswordEnabled, Segment,
		SecurityStamp2, PasswordHash2, SetPassword, IsBlocked, AuthenticatorKey
	from a2security.Users u
	where Void = 0 and Id <> 0;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2sys' and TABLE_NAME=N'Applications')
create table a2sys.[Applications]
(
	TenantId int not null,
	Id int not null,
	[Uid] uniqueidentifier not null,
	[Name] sysname not null,
	[Version] int not null,
	[Description] nvarchar(255),
	[Copyright] nvarchar(255),
	IsDevelopment bit not null,
	constraint PK_Applications primary key (TenantId, Id),
	constraint FK_Applications_TenantId_Tenants foreign key (TenantId) references a2security.Tenants(Id)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2ui' and TABLE_NAME=N'Modules')
create table a2ui.Modules
(
	Id uniqueidentifier not null,
	Parent uniqueidentifier null,
	[Name] nvarchar(255),
	[Memo] nvarchar(255),
	constraint PK_Modules primary key (Id),
	constraint FK_Modules_Parent_Modules foreign key (Parent) references a2ui.Modules(Id)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2ui' and TABLE_NAME=N'TenantModules')
create table a2ui.TenantModules
(
	Tenant int not null,
	Module uniqueidentifier not null,
	UtcDateCreated datetime not null constraint 
		DF_TenantModules_UtcDateCreated default(getutcdate()),
	constraint PK_TenantModules primary key (Tenant, Module),
	constraint FK_TenantModules_Tenant_Tenants foreign key (Tenant) references a2security.Tenants(Id),
	constraint FK_TenantModules_Module_Modules foreign key (Module) references a2ui.Modules(Id)
);
go
-------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2ui' and TABLE_NAME=N'ModuleInitProcedures')
create table a2ui.[ModuleInitProcedures]
(
	[Procedure] sysname,
	Module  uniqueidentifier not null,
	Memo nvarchar(255),
	constraint PK_ModuleInitProcedures primary key (Module, [Procedure]),
	constraint FK_ModuleInitProcedures_Module_Modules foreign key (Module) references a2ui.Modules(Id)
);
go
-------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2ui' and TABLE_NAME=N'TenantInitProcedures')
create table a2ui.[TenantInitProcedures]
(
	[Procedure] sysname,
	Module  uniqueidentifier not null,
	Memo nvarchar(255),
	constraint PK_TenantInitProcedures primary key (Module, [Procedure]),
	constraint FK_TenantInitProcedures_Module_Modules foreign key (Module) references a2ui.Modules(Id)
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2ui' and TABLE_NAME=N'Menu')
create table a2ui.Menu
(
	Tenant int not null,
	Id uniqueidentifier not null,
	Module uniqueidentifier not null,
	Parent uniqueidentifier,
	[Name] nvarchar(255),
	[Url] nvarchar(255),
	CreateName nvarchar(255),
	CreateUrl nvarchar(255),
	Icon nvarchar(255),
	[Order] int not null constraint DF_Menu_Order default(0),
	[ClassName] nvarchar(255) null,
	[IsDevelopment] bit constraint DF_Menu_IsDevelopment default(0),
	constraint PK_Menu primary key (Tenant, Id),
	constraint FK_Menu_Parent_Menu foreign key (Tenant, Parent) references a2ui.Menu(Tenant, Id),
	constraint FK_Menu_Module_Modules foreign key (Module) references a2ui.Modules(Id),
	constraint FK_Menu_Tenant_Module_TenantModules foreign key (Tenant, Module) references a2ui.TenantModules(Tenant, Module)
);
go
-- migrations
-------------------------------------------------
if (exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2security' and TABLE_NAME = N'ApiUserLogins' and COLUMN_NAME = N'ApiKey' and CHARACTER_MAXIMUM_LENGTH <> 1023))
	alter table a2security.ApiUserLogins alter column ApiKey nvarchar(1023);
go
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2security' and TABLE_NAME = N'Users' and COLUMN_NAME = N'IsApiUser')
	alter table a2security.Users add IsApiUser bit constraint DF_UsersIsApiUser default(0) with values;
go
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2security' and TABLE_NAME = N'Users' and COLUMN_NAME = N'IsExternalLogin')
	alter table a2security.Users add IsExternalLogin bit constraint DF_UsersIsExternalLogin default(0) with values;
go
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2ui' and TABLE_NAME = N'Menu' and COLUMN_NAME = N'IsDevelopment')
	alter table a2ui.Menu add [IsDevelopment] bit constraint DF_Menu_IsDevelopment default(0) with values;
go

